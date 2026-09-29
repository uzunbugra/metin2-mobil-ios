using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCHandshake (13 bytes).
    /// </summary>
    public static class PacketGCHandshakeCodec
    {
        public static int Serialize(in PacketGCHandshake packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCHandshake.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCHandshake.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.Handshake);
            writer.WriteUInt32(packet.Time);
            writer.WriteInt32(packet.Delta);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCHandshake packet)
        {
            byte[] buffer = new byte[PacketGCHandshake.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCHandshake packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCHandshake.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCHandshake.PacketSize} bytes for TPacketGCHandshake, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCHandshake.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCHandshake.PacketHeader:X2} (HEADER_GC_HANDSHAKE), got 0x{header:X2}.";
                return false;
            }

            uint handshake = reader.ReadUInt32();
            uint time = reader.ReadUInt32();
            int delta = reader.ReadInt32();

            packet = new PacketGCHandshake(handshake, time, delta);
            return true;
        }

        public static PacketGCHandshake Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCHandshake.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCHandshake.PacketSize, source.Length, nameof(PacketGCHandshake));
            }

            byte header = source[0];
            if (header != PacketGCHandshake.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCHandshake.PacketHeader, header, nameof(PacketGCHandshake));
            }

            if (!TryDeserialize(source, out PacketGCHandshake packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCHandshake));
            }

            return packet;
        }
    }
}
