using System;

namespace Metin2.Core.Logging
{
    /// <summary>
    /// Logging abstraction decoupling network and core systems from engine logging.
    /// </summary>
    public interface ILogger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception exception = null);
    }
}
