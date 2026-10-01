using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCAuthSuccess (6 bytes).
    /// References:
    /// Server: game/src/packet.h:849-854, input_db.cpp:1686-1710.
    /// Client: UserInterface/Packet.h:2370-2375, AccountConnector.cpp:312-337.
    /// </summary>
    public static class PacketGCAuthSuccessCodec
    {
        public static int Serialize(in PacketGCAuthSuccess packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCAuthSuccess.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCAuthSuccess.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.LoginKey);
            writer.WriteByte(packet.Result);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCAuthSuccess packet)
        {
            byte[] buffer = new byte[PacketGCAuthSuccess.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCAuthSuccess packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCAuthSuccess.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCAuthSuccess.PacketSize} bytes for TPacketGCAuthSuccess, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCAuthSuccess.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCAuthSuccess.PacketHeader:X2} (HEADER_GC_AUTH_SUCCESS), got 0x{header:X2}.";
                return false;
            }

            uint loginKey = reader.ReadUInt32();
            byte result = reader.ReadByte();

            packet = new PacketGCAuthSuccess(loginKey, result);
            return true;
        }

        public static PacketGCAuthSuccess Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCAuthSuccess.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCAuthSuccess.PacketSize, source.Length, nameof(PacketGCAuthSuccess));
            }

            byte header = source[0];
            if (header != PacketGCAuthSuccess.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCAuthSuccess.PacketHeader, header, nameof(PacketGCAuthSuccess));
            }

            if (!TryDeserialize(source, out PacketGCAuthSuccess packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCAuthSuccess));
            }

            return packet;
        }
    }
}
