using System;

namespace Metin2.Protocol.Exceptions
{
    /// <summary>
    /// Thrown when the packet header byte does not match the expected header constant.
    /// Indicates stream corruption, protocol desynchronization, or unauthorized packet injection.
    /// </summary>
    public class InvalidPacketHeaderException : PacketParseException
    {
        public byte ExpectedHeader { get; }
        public byte ActualHeader { get; }

        public InvalidPacketHeaderException(byte expectedHeader, byte actualHeader, string packetName = null)
            : base($"Invalid header: expected 0x{expectedHeader:X2} for {packetName ?? "packet"}, got 0x{actualHeader:X2}.",
                   packetName)
        {
            ExpectedHeader = expectedHeader;
            ActualHeader = actualHeader;
        }
    }
}
