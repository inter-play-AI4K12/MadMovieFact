using System;
using MadFact.Telemetry;
using UnityEditor;
using UnityEngine;

namespace MadFact.Editor
{
    [InitializeOnLoad]
    public static class RunTelemetrySmokeTest
    {
        const string PendingKey = "MadFact.TelemetrySmoke.Pending";
        const string ParticipantKey = "MadFact.TelemetrySmoke.Participant";
        const string SessionKey = "MadFact.TelemetrySmoke.Session";
        static double _deadline;
        static bool _waiting;

        static RunTelemetrySmokeTest()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("MadFact/Run Loki Smoke Test")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MadFactTelemetrySmoke] Stop Play Mode before starting a smoke test.");
                return;
            }

            string participantId = "codex_loki_test_" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
            SessionState.SetString(ParticipantKey, participantId);
            SessionState.SetBool(PendingKey, true);
            Debug.Log("[MadFactTelemetrySmoke] Starting consented Loki smoke test for " + participantId);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                return;

            SessionState.SetBool(PendingKey, false);
            if (MadFactSessionManager.Instance == null)
                new GameObject("MadFact Session Manager").AddComponent<MadFactSessionManager>();
            if (MadFactLokiLogger.Instance == null)
                new GameObject("MadFact Loki Logger").AddComponent<MadFactLokiLogger>();

            string participantId = SessionState.GetString(ParticipantKey, string.Empty);
            ParticipantSession session = MadFactSessionManager.Instance.StartSession(
                "Codex Telemetry Test", participantId, true);
            SessionState.SetString(SessionKey, session.game_session_id);
            MadFactLokiLogger.Instance.Log("session_started", "Telemetry smoke-test session started",
                new { smoke_test = true });
            MadFactLokiLogger.Instance.Log("telemetry_smoke_test", "Unity sent a Loki smoke-test event",
                new { smoke_test = true });

            _deadline = EditorApplication.timeSinceStartup + 20d;
            _waiting = true;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (!_waiting) return;
            bool queueEmpty = MadFactLokiLogger.Instance == null ||
                              MadFactLokiLogger.Instance.QueuedEventCount == 0;
            bool timedOut = EditorApplication.timeSinceStartup >= _deadline;
            if (!queueEmpty && !timedOut) return;

            _waiting = false;
            EditorApplication.update -= Poll;
            string participantId = SessionState.GetString(ParticipantKey, string.Empty);
            string sessionId = SessionState.GetString(SessionKey, string.Empty);
            Debug.Log($"[MadFactTelemetrySmoke] Finished participant_id={participantId} " +
                      $"game_session_id={sessionId} queue_empty={queueEmpty}");
            EditorApplication.ExitPlaymode();
        }
    }
}
