using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCDamageInfo (10 bytes).
    /// Verified from Server packet.h:2093-2099 (Client Packet.h same struct).
    /// </summary>
    public static class PacketGCDamageInfoCodec
    {
        public static int Serialize(in PacketGCDamageInfo packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCDamageInfo.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCDamageInfo.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.Vid);
            writer.WriteByte(packet.Flag);
            writer.WriteInt32(packet.Damage);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCDamageInfo packet)
        {
            byte[] buffer = new byte[PacketGCDamageInfo.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCDamageInfo packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCDamageInfo.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCDamageInfo.PacketSize} bytes for TPacketGCDamageInfo, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCDamageInfo.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCDamageInfo.PacketHeader:X2} (HEADER_GC_DAMAGE_INFO), got 0x{header:X2}.";
                return false;
            }

            uint vid = reader.ReadUInt32();
            byte flag = reader.ReadByte();
            int damage = reader.ReadInt32();
            packet = new PacketGCDamageInfo(vid, flag, damage);
            return true;
        }

        public static PacketGCDamageInfo Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCDamageInfo.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCDamageInfo.PacketSize, source.Length, nameof(PacketGCDamageInfo));
            }

            if (source[0] != PacketGCDamageInfo.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCDamageInfo.PacketHeader, source[0], nameof(PacketGCDamageInfo));
            }

            if (!TryDeserialize(source, out PacketGCDamageInfo packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCDamageInfo));
            }

            return packet;
        }
    }
}
