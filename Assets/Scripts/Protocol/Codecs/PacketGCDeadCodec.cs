using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCDead (5 bytes).
    /// Verified from Server packet.h:1057-1061, Client Packet.h:1369-1373.
    /// </summary>
    public static class PacketGCDeadCodec
    {
        public static int Serialize(in PacketGCDead packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCDead.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCDead.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.Vid);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCDead packet)
        {
            byte[] buffer = new byte[PacketGCDead.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCDead packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCDead.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCDead.PacketSize} bytes for TPacketGCDead, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCDead.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCDead.PacketHeader:X2} (HEADER_GC_DEAD), got 0x{header:X2}.";
                return false;
            }

            uint vid = reader.ReadUInt32();
            packet = new PacketGCDead(vid);
            return true;
        }

        public static PacketGCDead Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCDead.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCDead.PacketSize, source.Length, nameof(PacketGCDead));
            }

            if (source[0] != PacketGCDead.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCDead.PacketHeader, source[0], nameof(PacketGCDead));
            }

            if (!TryDeserialize(source, out PacketGCDead packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCDead));
            }

            return packet;
        }
    }
}
