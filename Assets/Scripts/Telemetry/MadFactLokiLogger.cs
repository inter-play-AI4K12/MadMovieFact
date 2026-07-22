using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace MadFact.Telemetry
{
    [DefaultExecutionOrder(-9990)]
    public sealed class MadFactLokiLogger : MonoBehaviour
    {
        sealed class PendingEvent
        {
            public string Payload;
            public string EventType;
            public int Attempts;
        }

        public static MadFactLokiLogger Instance { get; private set; }
        readonly Queue<PendingEvent> _queue = new Queue<PendingEvent>();
        Coroutine _sender;
        string _password;
        long _lastTimestampNanoseconds;
        bool _shuttingDown;
        bool _remoteAllowed;
        bool _warnedMissingCredentials;

        public int QueuedEventCount => _queue.Count;
        public bool HasRuntimeCredentials =>
            MadFactTelemetryConfig.UsesWebGlRelay || !string.IsNullOrEmpty(_password);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance == null)
                new GameObject("MadFact Loki Logger").AddComponent<MadFactLokiLogger>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _password = MadFactTelemetryConfig.ResolvePassword();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceived += OnUnityLog;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.logMessageReceived -= OnUnityLog;
            Instance = null;
        }

        public void Log(string eventType, string message, object data = null)
        {
            eventType = NormalizeEventType(eventType);
            var session = MadFactSessionManager.Instance?.CurrentSession;
            if (session == null || !session.logging_consent)
            {
                string localData = data == null ? string.Empty : " " + TelemetryJson.Serialize(data);
                Debug.Log($"[MadFactLocal] {eventType}: {message}{localData}");
                return;
            }
            _remoteAllowed = true;

            var record = new Dictionary<string, object>
            {
                ["app"] = "madfact",
                ["source"] = "unity",
                ["event_type"] = eventType,
                ["message"] = Truncate(message, 512),
                ["participant_id"] = session.participant_id,
                ["display_name"] = session.display_name,
                ["game_session_id"] = session.game_session_id,
                ["timestamp_utc"] = DateTime.UtcNow.ToString("O"),
                ["game_version"] = session.game_version,
                ["unity_version"] = session.unity_version,
                ["platform"] = session.platform,
                ["scene_name"] = SceneManager.GetActiveScene().name,
                ["data"] = data
            };

            string timestamp = NextTimestampNanoseconds().ToString();
            string recordJson = TelemetryJson.Serialize(record);
            string payload = BuildLokiPayload(timestamp, recordJson);
            Enqueue(new PendingEvent { Payload = payload, EventType = eventType });
        }

        public static string BuildLokiPayload(string timestampNanoseconds, string recordJson)
            => "{\"streams\":[{\"stream\":{\"app\":\"madfact\",\"source\":\"unity\"}," +
               "\"values\":[[" + TelemetryJson.Quote(timestampNanoseconds) + "," +
               TelemetryJson.Quote(recordJson) + "]]}]}";

        public static string NormalizeEventType(string eventType)
        {
            string normalized = TelemetryJson.ToSnakeCase(eventType);
            if (string.IsNullOrEmpty(normalized)) return "unknown";
            return normalized.Length <= 64 ? normalized : normalized.Substring(0, 64).TrimEnd('_');
        }

        public void StopRemoteLoggingAndClearQueue()
        {
            _remoteAllowed = false;
            _queue.Clear();
            if (_sender != null)
            {
                StopCoroutine(_sender);
                _sender = null;
            }
        }

        void Enqueue(PendingEvent pending)
        {
            if (_queue.Count >= MadFactTelemetryConfig.QueueCapacity)
            {
                _queue.Dequeue();
                Debug.LogWarning("[MadFactTelemetry] Queue capacity reached; oldest event dropped.");
            }
            _queue.Enqueue(pending);
            if (_sender == null && !_shuttingDown) _sender = StartCoroutine(SenderLoop());
        }

        IEnumerator SenderLoop()
        {
            while (_queue.Count > 0)
            {
                if (!CanTransmit())
                {
                    _queue.Clear();
                    break;
                }

                PendingEvent pending = _queue.Peek();
                using (var request = new UnityWebRequest(
                    MadFactTelemetryConfig.Endpoint, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(pending.Payload));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = MadFactTelemetryConfig.RequestTimeoutSeconds;
                    request.SetRequestHeader("Content-Type", "application/json");
                    if (!MadFactTelemetryConfig.UsesWebGlRelay)
                    {
                        string credentials = Convert.ToBase64String(
                            Encoding.UTF8.GetBytes(MadFactTelemetryConfig.Username + ":" + _password));
                        request.SetRequestHeader("Authorization", "Basic " + credentials);
                    }

                    yield return request.SendWebRequest();

                    bool success = request.result == UnityWebRequest.Result.Success &&
                                   request.responseCode >= 200 && request.responseCode < 300;
                    if (success)
                    {
                        _queue.Dequeue();
                        continue;
                    }

                    bool retryable = request.result == UnityWebRequest.Result.ConnectionError ||
                                     MadFactTelemetryConfig.IsRetryableStatus(request.responseCode);
                    pending.Attempts++;
                    if (!retryable || pending.Attempts > MadFactTelemetryConfig.MaxRetryCount)
                    {
                        _queue.Dequeue();
                        Debug.LogWarning($"[MadFactTelemetry] Dropped '{pending.EventType}' after HTTP " +
                            $"{request.responseCode}; gameplay continues.");
                        continue;
                    }
                }

                float delay = Mathf.Min(16f, Mathf.Pow(2f, pending.Attempts - 1));
                yield return new WaitForSecondsRealtime(delay);
            }
            _sender = null;
            if (MadFactSessionManager.Instance?.HasActiveSession != true)
                _remoteAllowed = false;
        }

        bool CanTransmit()
        {
            var session = MadFactSessionManager.Instance?.CurrentSession;
            if (session != null && !session.logging_consent) return false;
            if (!_remoteAllowed) return false;
            if (MadFactTelemetryConfig.UsesWebGlRelay) return true;
            if (!string.IsNullOrEmpty(_password)) return true;
            if (!_warnedMissingCredentials)
            {
                _warnedMissingCredentials = true;
                Debug.LogWarning("[MadFactTelemetry] LOKI_PASSWORD is not configured; remote events were dropped.");
            }
            return false;
        }

        long NextTimestampNanoseconds()
        {
            long current = (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) * 100L;
            if (current <= _lastTimestampNanoseconds) current = _lastTimestampNanoseconds + 1;
            _lastTimestampNanoseconds = current;
            return current;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            => Log("scene_changed", "Scene loaded", new { scene_name = scene.name, load_mode = mode.ToString() });

        void OnApplicationFocus(bool focused)
            => Log(focused ? "application_focus_gained" : "application_focus_lost",
                focused ? "Application regained focus" : "Application lost focus");

        void OnApplicationPause(bool paused)
            => Log(paused ? "pause_started" : "pause_ended",
                paused ? "Application paused" : "Application resumed");

        void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition != null && condition.StartsWith("[MadFactTelemetry]", StringComparison.Ordinal)) return;
            Log("error_occurred", "Unity reported an error", new
            {
                log_type = type.ToString(),
                error = Truncate(condition, 512),
                stack_trace = Truncate(stackTrace, 2048)
            });
        }

        void OnApplicationQuit()
        {
            _shuttingDown = true;
            if (MadFactSessionManager.Instance?.HasActiveSession == true)
                MadFactSessionManager.Instance.EndSession("application_quit");
            // Unity may allow an in-flight request to finish during shutdown. We never
            // block the main thread or hold the process open for telemetry.
            if (_sender == null && _queue.Count > 0 && CanTransmit())
                _sender = StartCoroutine(SenderLoop());
        }

        static string Truncate(string value, int maximum)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maximum) return value ?? string.Empty;
            return value.Substring(0, maximum);
        }
    }
}
