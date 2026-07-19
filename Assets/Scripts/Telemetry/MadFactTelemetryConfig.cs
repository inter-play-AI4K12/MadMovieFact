using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MadFact.Telemetry
{
    public static class MadFactTelemetryConfig
    {
        public const string Endpoint = "https://loki-madfact.interplaylab.io/loki/api/v1/push";
        public const string Username = "beetrap";
        public const int QueueCapacity = 500;
        public const int RequestTimeoutSeconds = 10;
        public const int MaxRetryCount = 4;

        [Serializable]
        sealed class LocalSecrets
        {
            public string loki_password;
        }

        /// <summary>
        /// Development builds use LOKI_PASSWORD from the process environment. A local
        /// persistent-data file is also supported for players that cannot inherit an
        /// environment: &lt;persistentDataPath&gt;/madfact.telemetry.local.json.
        /// That file is deliberately outside Assets and must never be committed.
        /// Production deployments should inject a short-lived secret through their
        /// platform bootstrap rather than packaging it with the client.
        /// </summary>
        public static string ResolvePassword()
        {
            string value = Environment.GetEnvironmentVariable("LOKI_PASSWORD");
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

            try
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                foreach (string envPath in DevelopmentEnvPaths())
                {
                    value = ReadDotEnvValue(envPath, "LOKI_PASSWORD");
                    if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                }
#endif
                string path = Path.Combine(Application.persistentDataPath, "madfact.telemetry.local.json");
                if (!File.Exists(path)) return null;
                var local = JsonUtility.FromJson<LocalSecrets>(File.ReadAllText(path));
                return string.IsNullOrWhiteSpace(local?.loki_password) ? null : local.loki_password.Trim();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[MadFactTelemetry] Could not read local runtime configuration: " +
                    exception.GetType().Name);
                return null;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static IEnumerable<string> DevelopmentEnvPaths()
        {
            // In the Unity Editor, Application.dataPath points at <project>/Assets.
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
                yield return Path.Combine(projectRoot, ".env");

            // Supports desktop development players launched from a directory containing
            // their local .env without ever packaging that file into the build.
            yield return Path.Combine(Directory.GetCurrentDirectory(), ".env");
        }

        static string ReadDotEnvValue(string path, string key)
        {
            if (!File.Exists(path)) return null;
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("export ", StringComparison.Ordinal))
                    line = line.Substring(7).TrimStart();
                int separator = line.IndexOf('=');
                if (separator <= 0 || !string.Equals(line.Substring(0, separator).Trim(), key,
                    StringComparison.Ordinal)) continue;
                string parsed = line.Substring(separator + 1).Trim();
                if (parsed.Length >= 2 &&
                    ((parsed[0] == '"' && parsed[parsed.Length - 1] == '"') ||
                     (parsed[0] == '\'' && parsed[parsed.Length - 1] == '\'')))
                    parsed = parsed.Substring(1, parsed.Length - 2);
                return parsed;
            }
            return null;
        }
#endif

        public static bool IsRetryableStatus(long statusCode)
            => statusCode == 0 || statusCode == 408 || statusCode == 429 ||
               (statusCode >= 500 && statusCode <= 599);
    }
}
