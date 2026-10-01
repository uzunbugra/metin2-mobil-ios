using System;
using System.Text;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCLoginFailure (10 bytes).
    /// References:
    /// Server: game/src/packet.h:856-860, common/length.h:12, input.cpp:177-188.
    /// Client: UserInterface/Packet.h:1136-1141, PhaseLogin.cpp:204-212,
    /// AccountConnector.cpp:340-352.
    /// </summary>
    public static class PacketGCLoginFailureCodec
    {
        public static int Serialize(in PacketGCLoginFailure packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCLoginFailure.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCLoginFailure.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteFixedString(packet.Status, PacketGCLoginFailure.StatusBufferLen, Encoding.ASCII);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCLoginFailure packet)
        {
            byte[] buffer = new byte[PacketGCLoginFailure.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCLoginFailure packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCLoginFailure.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCLoginFailure.PacketSize} bytes for TPacketGCLoginFailure, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCLoginFailure.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCLoginFailure.PacketHeader:X2} (HEADER_GC_LOGIN_FAILURE), got 0x{header:X2}.";
                return false;
            }

            string status = reader.ReadFixedString(PacketGCLoginFailure.StatusBufferLen, Encoding.ASCII);

            packet = new PacketGCLoginFailure(status);
            return true;
        }

        public static PacketGCLoginFailure Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCLoginFailure.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCLoginFailure.PacketSize, source.Length, nameof(PacketGCLoginFailure));
            }

            byte header = source[0];
            if (header != PacketGCLoginFailure.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCLoginFailure.PacketHeader, header, nameof(PacketGCLoginFailure));
            }

            if (!TryDeserialize(source, out PacketGCLoginFailure packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCLoginFailure));
            }

            return packet;
        }
    }
}
