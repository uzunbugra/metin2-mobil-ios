using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketCGItemMove (8 bytes).
    /// Verified from Server packet.h:660-666, Client Packet.h (same struct).
    /// </summary>
    public static class PacketCGItemMoveCodec
    {
        public static int Serialize(in PacketCGItemMove packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGItemMove.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGItemMove.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            ItemFieldCodec.WriteCell(ref writer, packet.Window, packet.Cell);
            ItemFieldCodec.WriteCell(ref writer, packet.WindowTo, packet.CellTo);
            writer.WriteByte(packet.Count);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGItemMove packet)
        {
            byte[] buffer = new byte[PacketCGItemMove.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGItemMove packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGItemMove.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGItemMove.PacketSize} bytes for TPacketCGItemMove, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGItemMove.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGItemMove.PacketHeader:X2} (HEADER_CG_ITEM_MOVE), got 0x{header:X2}.";
                return false;
            }

            ItemFieldCodec.ReadCell(ref reader, out byte window, out ushort cell);
            ItemFieldCodec.ReadCell(ref reader, out byte windowTo, out ushort cellTo);
            byte count = reader.ReadByte();
            packet = new PacketCGItemMove(window, cell, windowTo, cellTo, count);
            return true;
        }

        public static PacketCGItemMove Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGItemMove.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGItemMove.PacketSize, source.Length, nameof(PacketCGItemMove));
            }

            if (source[0] != PacketCGItemMove.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGItemMove.PacketHeader, source[0], nameof(PacketCGItemMove));
            }

            if (!TryDeserialize(source, out PacketCGItemMove packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGItemMove));
            }

            return packet;
        }
    }
}
