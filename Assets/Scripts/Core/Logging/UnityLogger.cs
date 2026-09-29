using System;
#if UNITY_5_3_OR_NEWER || UNITY_EDITOR
using UnityEngine;
#endif

namespace Metin2.Core.Logging
{
    /// <summary>
    /// Unity engine logger implementation with integrated SecretRedactor protection.
    /// Supports fallback to Console output when compiled in standalone test runners.
    /// </summary>
    public class UnityLogger : ILogger
    {
        private readonly string _tag;

        public UnityLogger(string tag = "Metin2")
        {
            _tag = tag;
        }

        public static string FormatLogMessage(string tag, string message, Exception exception = null)
        {
            string fullMessage = exception != null
                ? $"[{tag}] {message}\nException: {exception}"
                : $"[{tag}] {message}";

            return SecretRedactor.Redact(fullMessage);
        }

        public void LogInfo(string message)
        {
            string fullMessage = FormatLogMessage(_tag, message);
#if UNITY_5_3_OR_NEWER || UNITY_EDITOR
            Debug.Log(fullMessage);
#else
            Console.WriteLine($"[{_tag}][INFO] {fullMessage}");
#endif
        }

        public void LogWarning(string message)
        {
            string fullMessage = FormatLogMessage(_tag, message);
#if UNITY_5_3_OR_NEWER || UNITY_EDITOR
            Debug.LogWarning(fullMessage);
#else
            Console.WriteLine($"[{_tag}][WARN] {fullMessage}");
#endif
        }

        public void LogError(string message, Exception exception = null)
        {
            string fullMessage = FormatLogMessage(_tag, message, exception);

#if UNITY_5_3_OR_NEWER || UNITY_EDITOR
            Debug.LogError(fullMessage);
#else
            Console.Error.WriteLine($"[{_tag}][ERROR] {fullMessage}");
#endif
        }
    }
}
