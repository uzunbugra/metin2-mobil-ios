using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCMotion (11 bytes).
    /// Verified from Server packet.h:1158-1164, Client Packet.h:1617-1623.
    /// </summary>
    public static class PacketGCMotionCodec
    {
        public static int Serialize(in PacketGCMotion packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCMotion.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCMotion.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.Vid);
            writer.WriteUInt32(packet.VictimVid);
            writer.WriteUInt16(packet.Motion);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCMotion packet)
        {
            byte[] buffer = new byte[PacketGCMotion.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCMotion packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCMotion.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCMotion.PacketSize} bytes for TPacketGCMotion, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCMotion.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCMotion.PacketHeader:X2} (HEADER_GC_MOTION), got 0x{header:X2}.";
                return false;
            }

            uint vid = reader.ReadUInt32();
            uint victimVid = reader.ReadUInt32();
            ushort motion = reader.ReadUInt16();
            packet = new PacketGCMotion(vid, victimVid, motion);
            return true;
        }

        public static PacketGCMotion Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCMotion.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCMotion.PacketSize, source.Length, nameof(PacketGCMotion));
            }

            if (source[0] != PacketGCMotion.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCMotion.PacketHeader, source[0], nameof(PacketGCMotion));
            }

            if (!TryDeserialize(source, out PacketGCMotion packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCMotion));
            }

            return packet;
        }
    }
}
