using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MadFact.Telemetry
{
    [DefaultExecutionOrder(-10000)]
    public sealed class MadFactSessionManager : MonoBehaviour
    {
        const string AnonymousParticipantKey = "madfact.anonymous_participant_id";
        static readonly Regex SafeParticipantId = new Regex(
            "^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$",
            RegexOptions.CultureInvariant);

        public static MadFactSessionManager Instance { get; private set; }
        public bool HasActiveSession => CurrentSession != null;
        public ParticipantSession CurrentSession { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance == null)
                new GameObject("MadFact Session Manager").AddComponent<MadFactSessionManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public ParticipantSession StartSession(string displayName, string suppliedParticipantId, bool consent)
        {
            if (HasActiveSession) throw new InvalidOperationException("A participant session is already active.");
            displayName = (displayName ?? string.Empty).Trim();
            suppliedParticipantId = (suppliedParticipantId ?? string.Empty).Trim();

            string error = Validate(displayName, suppliedParticipantId, consent);
            if (error != null) throw new ArgumentException(error);

            string participantId = string.IsNullOrEmpty(suppliedParticipantId)
                ? GetOrCreateAnonymousParticipantId()
                : suppliedParticipantId;

            CurrentSession = new ParticipantSession
            {
                participant_id = participantId,
                display_name = displayName,
                game_session_id = Guid.NewGuid().ToString("D"),
                logging_consent = consent,
                session_started_at_utc = DateTime.UtcNow.ToString("O"),
                game_version = Application.version,
                unity_version = Application.unityVersion,
                platform = Application.platform.ToString(),
                operating_system_family = SystemInfo.operatingSystemFamily.ToString(),
                device_type = SystemInfo.deviceType.ToString(),
                system_language = Application.systemLanguage.ToString()
            };
            return CurrentSession;
        }

        public void EndSession(string reason = "completed")
        {
            if (!HasActiveSession) return;
            MadFactLokiLogger.Instance?.Log("session_ended", "Participant session ended",
                new { reason, duration_ms = SessionDurationMilliseconds() });
            CurrentSession = null;
        }

        public void WithdrawConsent()
        {
            if (!HasActiveSession || !CurrentSession.logging_consent) return;
            CurrentSession.logging_consent = false;
            MadFactLokiLogger.Instance?.StopRemoteLoggingAndClearQueue();
        }

        public long SessionDurationMilliseconds()
        {
            if (!HasActiveSession ||
                !DateTime.TryParse(CurrentSession.session_started_at_utc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime started))
                return 0;
            return Math.Max(0, (long)(DateTime.UtcNow - started.ToUniversalTime()).TotalMilliseconds);
        }

        public static string Validate(string displayName, string participantId, bool consent)
        {
            displayName = (displayName ?? string.Empty).Trim();
            participantId = (participantId ?? string.Empty).Trim();
            if (displayName.Length == 0) return "Please enter your display name.";
            if (displayName.Length > 50) return "Display name must be 50 characters or fewer.";
            if (participantId.Length > 0 && !SafeParticipantId.IsMatch(participantId))
                return "The participant ID contains unsupported characters.";
            if (!consent) return "Please review and accept the logging consent.";
            return null;
        }

        public static bool IsParticipantIdValid(string participantId)
        {
            participantId = (participantId ?? string.Empty).Trim();
            return participantId.Length == 0 || SafeParticipantId.IsMatch(participantId);
        }

        static string GetOrCreateAnonymousParticipantId()
        {
            string existing = PlayerPrefs.GetString(AnonymousParticipantKey, string.Empty);
            if (SafeParticipantId.IsMatch(existing)) return existing;
            string generated = "anon_" + Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(AnonymousParticipantKey, generated);
            PlayerPrefs.Save();
            return generated;
        }
    }
}
