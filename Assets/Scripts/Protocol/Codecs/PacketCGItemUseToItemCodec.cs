using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketCGItemUseToItem (7 bytes).
    /// Verified from Server packet.h:638-643, Client Packet.h (same struct).
    /// </summary>
    public static class PacketCGItemUseToItemCodec
    {
        public static int Serialize(in PacketCGItemUseToItem packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGItemUseToItem.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGItemUseToItem.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            ItemFieldCodec.WriteCell(ref writer, packet.Window, packet.Cell);
            ItemFieldCodec.WriteCell(ref writer, packet.TargetWindow, packet.TargetCell);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGItemUseToItem packet)
        {
            byte[] buffer = new byte[PacketCGItemUseToItem.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGItemUseToItem packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGItemUseToItem.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGItemUseToItem.PacketSize} bytes for TPacketCGItemUseToItem, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGItemUseToItem.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGItemUseToItem.PacketHeader:X2} (HEADER_CG_ITEM_USE_TO_ITEM), got 0x{header:X2}.";
                return false;
            }

            ItemFieldCodec.ReadCell(ref reader, out byte window, out ushort cell);
            ItemFieldCodec.ReadCell(ref reader, out byte targetWindow, out ushort targetCell);
            packet = new PacketCGItemUseToItem(window, cell, targetWindow, targetCell);
            return true;
        }

        public static PacketCGItemUseToItem Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGItemUseToItem.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGItemUseToItem.PacketSize, source.Length, nameof(PacketCGItemUseToItem));
            }

            if (source[0] != PacketCGItemUseToItem.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGItemUseToItem.PacketHeader, source[0], nameof(PacketCGItemUseToItem));
            }

            if (!TryDeserialize(source, out PacketCGItemUseToItem packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGItemUseToItem));
            }

            return packet;
        }
    }
}
