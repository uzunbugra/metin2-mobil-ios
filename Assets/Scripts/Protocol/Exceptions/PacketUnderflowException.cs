using System;

namespace Metin2.Protocol.Exceptions
{
    /// <summary>
    /// Thrown when an input buffer contains fewer bytes than required by the packet definition.
    /// Indicates a partial frame in stream-based TCP framing.
    /// </summary>
    public class PacketUnderflowException : PacketException
    {
        public int ExpectedBytes { get; }
        public int ActualBytes { get; }

        public PacketUnderflowException(int expectedBytes, int actualBytes, string packetName = null)
            : base($"Buffer underflow: expected at least {expectedBytes} bytes for {packetName ?? "packet"}, got {actualBytes}.",
                   nameof(expectedBytes), packetName)
        {
            ExpectedBytes = expectedBytes;
            ActualBytes = actualBytes;
        }
    }
}
