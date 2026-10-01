using System;

namespace Metin2.Protocol.Exceptions
{
    /// <summary>
    /// Handshake / key-agreement failure (fail-closed).
    /// Thrown when the server breaks the expected GC 0xff → GC 0xfb → CG 0xfb → GC 0xfa
    /// sequence, sends bad lengths/keys, or closes mid-handshake.
    /// No UnityEngine dependency.
    /// </summary>
    public class HandshakeFailedException : PacketException
    {
        public HandshakeFailedException(string message)
            : base(message, "HandshakeClient")
        {
        }

        public HandshakeFailedException(string message, Exception innerException)
            : base(message, "HandshakeClient", innerException)
        {
        }
    }
}
