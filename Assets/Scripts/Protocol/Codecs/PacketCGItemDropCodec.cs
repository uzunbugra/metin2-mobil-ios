using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketCGItemDrop (8 bytes).
    /// Verified from Server packet.h:645-650, Client Packet.h (same struct).
    /// </summary>
    public static class PacketCGItemDropCodec
    {
        public static int Serialize(in PacketCGItemDrop packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGItemDrop.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGItemDrop.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            ItemFieldCodec.WriteCell(ref writer, packet.Window, packet.Cell);
            writer.WriteUInt32(packet.Gold);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGItemDrop packet)
        {
            byte[] buffer = new byte[PacketCGItemDrop.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGItemDrop packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGItemDrop.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGItemDrop.PacketSize} bytes for TPacketCGItemDrop, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGItemDrop.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGItemDrop.PacketHeader:X2} (HEADER_CG_ITEM_DROP), got 0x{header:X2}.";
                return false;
            }

            ItemFieldCodec.ReadCell(ref reader, out byte window, out ushort cell);
            uint gold = reader.ReadUInt32();
            packet = new PacketCGItemDrop(window, cell, gold);
            return true;
        }

        public static PacketCGItemDrop Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGItemDrop.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGItemDrop.PacketSize, source.Length, nameof(PacketCGItemDrop));
            }

            if (source[0] != PacketCGItemDrop.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGItemDrop.PacketHeader, source[0], nameof(PacketCGItemDrop));
            }

            if (!TryDeserialize(source, out PacketCGItemDrop packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGItemDrop));
            }

            return packet;
        }
    }
}
