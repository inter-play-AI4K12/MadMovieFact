using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MadFact.Telemetry
{
    /// <summary>
    /// Stores optional follow-up contact details outside ParticipantSession and telemetry payloads.
    /// A deployment can later replace this local store with a dedicated contact endpoint.
    /// </summary>
    public static class FollowUpContactStore
    {
        const string EmailKey = "madfact.follow_up.email";
        const string ConsentKey = "madfact.follow_up.consent";
        static readonly Regex EmailPattern = new Regex(
            @"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant);

        public static void Save(string email, bool consent)
        {
            email = (email ?? string.Empty).Trim();
            bool optedIn = consent && email.Length > 0;
            PlayerPrefs.SetString(EmailKey, optedIn ? email : string.Empty);
            PlayerPrefs.SetInt(ConsentKey, optedIn ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void Load(out string email, out bool consent)
        {
            email = PlayerPrefs.GetString(EmailKey, string.Empty);
            consent = PlayerPrefs.GetInt(ConsentKey, 0) == 1 && email.Length > 0;
        }

        public static string Validate(string email, bool consent)
        {
            email = (email ?? string.Empty).Trim();
            if (email.Length == 0)
                return consent ? "Enter an email address or turn off follow-up consent." : null;
            if (!EmailPattern.IsMatch(email) || email.Length > 254)
                return "Enter a valid follow-up email address.";
            if (!consent)
                return "To save an email, opt in to study follow-up.";
            return null;
        }
    }
}
