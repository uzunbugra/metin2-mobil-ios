using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCStun (5 bytes).
    /// Verified from Server packet.h:1051-1055, Client Packet.h:1363-1367.
    /// </summary>
    public static class PacketGCStunCodec
    {
        public static int Serialize(in PacketGCStun packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCStun.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCStun.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.Vid);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCStun packet)
        {
            byte[] buffer = new byte[PacketGCStun.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCStun packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCStun.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCStun.PacketSize} bytes for TPacketGCStun, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCStun.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCStun.PacketHeader:X2} (HEADER_GC_STUN), got 0x{header:X2}.";
                return false;
            }

            uint vid = reader.ReadUInt32();
            packet = new PacketGCStun(vid);
            return true;
        }

        public static PacketGCStun Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCStun.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCStun.PacketSize, source.Length, nameof(PacketGCStun));
            }

            if (source[0] != PacketGCStun.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCStun.PacketHeader, source[0], nameof(PacketGCStun));
            }

            if (!TryDeserialize(source, out PacketGCStun packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCStun));
            }

            return packet;
        }
    }
}
