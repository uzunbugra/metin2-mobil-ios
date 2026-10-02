using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketCGItemUse (4 bytes).
    /// Verified from Server packet.h:632-636, Client Packet.h (same struct).
    /// </summary>
    public static class PacketCGItemUseCodec
    {
        public static int Serialize(in PacketCGItemUse packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGItemUse.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGItemUse.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            ItemFieldCodec.WriteCell(ref writer, packet.Window, packet.Cell);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGItemUse packet)
        {
            byte[] buffer = new byte[PacketCGItemUse.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGItemUse packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGItemUse.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGItemUse.PacketSize} bytes for TPacketCGItemUse, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGItemUse.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGItemUse.PacketHeader:X2} (HEADER_CG_ITEM_USE), got 0x{header:X2}.";
                return false;
            }

            ItemFieldCodec.ReadCell(ref reader, out byte window, out ushort cell);
            packet = new PacketCGItemUse(window, cell);
            return true;
        }

        public static PacketCGItemUse Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGItemUse.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGItemUse.PacketSize, source.Length, nameof(PacketCGItemUse));
            }

            if (source[0] != PacketCGItemUse.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGItemUse.PacketHeader, source[0], nameof(PacketCGItemUse));
            }

            if (!TryDeserialize(source, out PacketCGItemUse packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGItemUse));
            }

            return packet;
        }
    }
}
