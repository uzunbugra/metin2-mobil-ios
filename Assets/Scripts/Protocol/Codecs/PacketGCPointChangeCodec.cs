using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCPointChange (17 bytes).
    /// Verified from Server packet.h:1042-1049, Client Packet.h:1606-1615.
    /// NOTE the int-header quirk: the header is a 4-byte int on the wire,
    /// unlike every other packet's 1-byte header (see the packet doc comment).
    /// </summary>
    public static class PacketGCPointChangeCodec
    {
        public static int Serialize(in PacketGCPointChange packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCPointChange.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCPointChange.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteInt32(packet.Header); // int header quirk: 4-byte header on the wire
            writer.WriteUInt32(packet.Vid);
            writer.WriteByte(packet.Type);
            writer.WriteInt32(packet.Amount);
            writer.WriteInt32(packet.Value);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCPointChange packet)
        {
            byte[] buffer = new byte[PacketGCPointChange.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCPointChange packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCPointChange.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCPointChange.PacketSize} bytes for TPacketGCPointChange, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            int header = reader.ReadInt32();
            if (header != PacketGCPointChange.PacketHeader)
            {
                errorMessage = $"Invalid header: expected int {PacketGCPointChange.PacketHeader} (HEADER_GC_POINT_CHANGE), got {header}.";
                return false;
            }

            uint vid = reader.ReadUInt32();
            byte type = reader.ReadByte();
            int amount = reader.ReadInt32();
            int value = reader.ReadInt32();
            packet = new PacketGCPointChange(vid, type, amount, value);
            return true;
        }

        public static PacketGCPointChange Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCPointChange.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCPointChange.PacketSize, source.Length, nameof(PacketGCPointChange));
            }

            if (!TryDeserialize(source, out PacketGCPointChange packet, out string errorMessage))
            {
                int header = BitConverter.ToInt32(source.Slice(0, 4).ToArray(), 0);
                if (header != PacketGCPointChange.PacketHeader)
                {
                    throw new InvalidPacketHeaderException(PacketGCPointChange.PacketHeader, (byte)header, nameof(PacketGCPointChange));
                }

                throw new PacketParseException(errorMessage, nameof(PacketGCPointChange));
            }

            return packet;
        }
    }
}
