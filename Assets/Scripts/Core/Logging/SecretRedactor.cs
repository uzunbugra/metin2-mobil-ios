using System;
using System.Text.RegularExpressions;

namespace Metin2.Core.Logging
{
    /// <summary>
    /// Enforces AGENT_DEVELOPMENT_GUIDE.md § 0.1 rule 6:
    /// Never log or commit credentials, passwords, auth tokens, or client keys.
    /// Redacts known credential patterns from log strings before emission.
    /// </summary>
    public static class SecretRedactor
    {
        public const string RedactedReplacement = "[REDACTED]";

        // Pattern matching key-value pairs like password=xyz, passwd: 123, pwd = "abc", token=Bearer jwt
        private static readonly Regex KeyValueSecretPattern = new Regex(
            @"(?i)(password|passwd|pwd|pass|secret|token|auth_token|access_token|client_key|api_key|dwLoginKey)\s*([:=])\s*(""[^""]*""|'[^']*'|(?:Bearer\s+)?[^\s,;&]+)",
            RegexOptions.Compiled);

        // Pattern matching JSON style "password": "value"
        private static readonly Regex JsonSecretPattern = new Regex(
            @"(?i)""(password|passwd|pwd|pass|secret|token|auth_token|access_token|client_key|api_key|dwLoginKey)""\s*:\s*""([^""]+)""",
            RegexOptions.Compiled);

        // Pattern matching Bearer tokens
        private static readonly Regex BearerPattern = new Regex(
            @"(?i)Bearer\s+([A-Za-z0-9\-\._~\+\/]+=*)",
            RegexOptions.Compiled);

        /// <summary>
        /// Scans the input string and replaces sensitive credential values with [REDACTED].
        /// </summary>
        public static string Redact(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            string result = input;

            // Redact key-value pairs (e.g. password=secret -> password=[REDACTED], token=Bearer jwt -> token=Bearer [REDACTED])
            result = KeyValueSecretPattern.Replace(result, match =>
            {
                string key = match.Groups[1].Value;
                string delimiter = match.Groups[2].Value;
                string val = match.Groups[3].Value;
                if (val.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{key}{delimiter}Bearer {RedactedReplacement}";
                }
                return $"{key}{delimiter}{RedactedReplacement}";
            });

            // Redact JSON style ("password": "secret" -> "password": "[REDACTED]")
            result = JsonSecretPattern.Replace(result, match =>
            {
                string key = match.Groups[1].Value;
                return $"\"{key}\": \"{RedactedReplacement}\"";
            });

            // Redact Authorization: Bearer tokens
            result = BearerPattern.Replace(result, $"Bearer {RedactedReplacement}");

            return result;
        }

        /// <summary>
        /// Masks a string by replacing all characters with asterisks except optional prefix/suffix.
        /// </summary>
        public static string Mask(string sensitiveString, int visiblePrefix = 0, int visibleSuffix = 0)
        {
            if (string.IsNullOrEmpty(sensitiveString))
            {
                return string.Empty;
            }

            int length = sensitiveString.Length;
            if (length <= visiblePrefix + visibleSuffix)
            {
                return new string('*', length);
            }

            string prefix = visiblePrefix > 0 ? sensitiveString.Substring(0, visiblePrefix) : string.Empty;
            string suffix = visibleSuffix > 0 ? sensitiveString.Substring(length - visibleSuffix, visibleSuffix) : string.Empty;
            int maskedCount = length - prefix.Length - suffix.Length;

            return $"{prefix}{new string('*', maskedCount)}{suffix}";
        }
    }
}
