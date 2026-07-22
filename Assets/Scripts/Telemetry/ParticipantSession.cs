using System;

namespace MadFact.Telemetry
{
    [Serializable]
    public sealed class ParticipantSession
    {
        public string participant_id;
        public string display_name;
        public string game_session_id;
        public bool logging_consent;
        public string session_started_at_utc;
        public string game_version;
        public string unity_version;
        public string platform;
        public string operating_system_family;
        public string device_type;
        public string system_language;
    }
}
