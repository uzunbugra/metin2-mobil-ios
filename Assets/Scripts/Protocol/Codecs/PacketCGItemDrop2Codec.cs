using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketCGItemDrop2 (9 bytes).
    /// Verified from Server packet.h:652-658, Client Packet.h (same struct).
    /// </summary>
    public static class PacketCGItemDrop2Codec
    {
        public static int Serialize(in PacketCGItemDrop2 packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGItemDrop2.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGItemDrop2.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            ItemFieldCodec.WriteCell(ref writer, packet.Window, packet.Cell);
            writer.WriteUInt32(packet.Gold);
            writer.WriteByte(packet.Count);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGItemDrop2 packet)
        {
            byte[] buffer = new byte[PacketCGItemDrop2.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGItemDrop2 packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGItemDrop2.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGItemDrop2.PacketSize} bytes for TPacketCGItemDrop2, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGItemDrop2.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGItemDrop2.PacketHeader:X2} (HEADER_CG_ITEM_DROP2), got 0x{header:X2}.";
                return false;
            }

            ItemFieldCodec.ReadCell(ref reader, out byte window, out ushort cell);
            uint gold = reader.ReadUInt32();
            byte count = reader.ReadByte();
            packet = new PacketCGItemDrop2(window, cell, gold, count);
            return true;
        }

        public static PacketCGItemDrop2 Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGItemDrop2.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGItemDrop2.PacketSize, source.Length, nameof(PacketCGItemDrop2));
            }

            if (source[0] != PacketCGItemDrop2.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGItemDrop2.PacketHeader, source[0], nameof(PacketCGItemDrop2));
            }

            if (!TryDeserialize(source, out PacketCGItemDrop2 packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGItemDrop2));
            }

            return packet;
        }
    }
}
