using System;

namespace Metin2.Protocol.Exceptions
{
    /// <summary>
    /// Thrown when a negotiated cipher suite has no ported engine yet
    /// (see docs/sprints/SPRINT_03-cipher-engines.md for the checklist).
    /// </summary>
    public class CipherEngineNotImplementedException : NotSupportedException
    {
        public string SuiteName { get; }

        public CipherEngineNotImplementedException(string suiteName)
            : base($"No block-cipher engine ported for suite '{suiteName}' yet.")
        {
            SuiteName = suiteName;
        }

        public CipherEngineNotImplementedException(string suiteName, Exception innerException)
            : base($"No block-cipher engine ported for suite '{suiteName}' yet.", innerException)
        {
            SuiteName = suiteName;
        }
    }
}
