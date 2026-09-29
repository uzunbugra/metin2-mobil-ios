using System;

namespace Metin2.Protocol.Exceptions
{
    /// <summary>
    /// Base exception for structural or payload parsing failures in a packet.
    /// </summary>
    public class PacketParseException : PacketException
    {
        public PacketParseException(string message, string packetName = null, Exception innerException = null)
            : base(message, packetName, innerException)
        {
        }
    }
}
