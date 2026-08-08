using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MadFact.Telemetry
{
    [DefaultExecutionOrder(-10000)]
    public sealed class MadFactSessionManager : MonoBehaviour
    {
        const string AnonymousParticipantKey = "madfact.anonymous_participant_id";
        const string SavedDisplayNameKey = "madfact.profile.display_name";
        const string SavedParticipantIdKey = "madfact.profile.participant_id";
        const string SavedConsentKey = "madfact.profile.logging_consent";
        const string SavedProfileKey = "madfact.profile.saved";
        static readonly Regex SafeParticipantId = new Regex(
            "^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$",
            RegexOptions.CultureInvariant);

        public static MadFactSessionManager Instance { get; private set; }
        public bool HasActiveSession => CurrentSession != null;
        public ParticipantSession CurrentSession { get; private set; }
        public static bool HasSavedProfile => PlayerPrefs.GetInt(SavedProfileKey, 0) == 1;

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

        public static void SaveProfile(string displayName, string participantId, bool consent)
        {
            PlayerPrefs.SetString(SavedDisplayNameKey, (displayName ?? string.Empty).Trim());
            PlayerPrefs.SetString(SavedParticipantIdKey, (participantId ?? string.Empty).Trim());
            PlayerPrefs.SetInt(SavedConsentKey, consent ? 1 : 0);
            PlayerPrefs.SetInt(SavedProfileKey, 1);
            PlayerPrefs.Save();
        }

        public static void LoadSavedProfile(out string displayName, out string participantId,
            out bool consent)
        {
            displayName = PlayerPrefs.GetString(SavedDisplayNameKey, string.Empty);
            participantId = PlayerPrefs.GetString(SavedParticipantIdKey, string.Empty);
            consent = PlayerPrefs.GetInt(SavedConsentKey, 0) == 1;
        }

        public bool TryStartSavedSession()
        {
            if (HasActiveSession) return true;
            if (!HasSavedProfile) return false;
            LoadSavedProfile(out string displayName, out string participantId, out bool consent);
            if (Validate(displayName, participantId, consent) != null) return false;
            StartSession(displayName, participantId, consent);
            MadFactLokiLogger.Instance?.Log(
                "session_started",
                "Participant session started",
                new { anonymous_participant = string.IsNullOrEmpty(participantId) });
            return true;
        }

        public void ApplySavedProfileToSession()
        {
            if (!HasActiveSession || !HasSavedProfile) return;
            LoadSavedProfile(out string displayName, out string participantId, out bool consent);
            CurrentSession.display_name = displayName;
            if (!string.IsNullOrEmpty(participantId)) CurrentSession.participant_id = participantId;
            CurrentSession.logging_consent = consent;
            if (!consent) MadFactLokiLogger.Instance?.StopRemoteLoggingAndClearQueue();
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
