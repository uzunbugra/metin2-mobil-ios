using System;

namespace Metin2.Protocol.Exceptions
{
    /// <summary>
    /// Base exception for all Metin2 binary protocol serialization and deserialization errors.
    /// Inherits from ArgumentException for backward compatibility with general argument checking.
    /// </summary>
    public class PacketException : ArgumentException
    {
        public string PacketName { get; }

        public PacketException(string message, string packetName = null, Exception innerException = null)
            : base(message, innerException)
        {
            PacketName = packetName;
        }

        public PacketException(string message, string paramName, string packetName)
            : base(message, paramName)
        {
            PacketName = packetName;
        }
    }
}
