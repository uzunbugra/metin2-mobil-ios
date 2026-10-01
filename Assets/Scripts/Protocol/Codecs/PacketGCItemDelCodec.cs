using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for server TPacketGCItemDelDeprecated (42 bytes, header 20).
    /// Server: game/src/packet.h:1063-1071, char_item.cpp:426-437.
    /// Client: UserInterface/Packet.h:1663-1671, PhaseGameItem.cpp:214-236.
    /// </summary>
    public static class PacketGCItemDelCodec
    {
        public static int Serialize(in PacketGCItemDel packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCItemDel.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCItemDel.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            ItemFieldCodec.WriteCell(ref writer, packet.Window, packet.Cell);
            writer.WriteUInt32(packet.Vnum);
            writer.WriteByte(packet.Count);
            ItemFieldCodec.WriteSockets(ref writer, packet.Sockets);
            ItemFieldCodec.WriteAttributes(ref writer, packet.Attributes);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCItemDel packet)
        {
            byte[] buffer = new byte[PacketGCItemDel.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCItemDel packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCItemDel.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCItemDel.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCItemDel.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCItemDel.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            ItemFieldCodec.ReadCell(ref reader, out byte window, out ushort cell);
            uint vnum = reader.ReadUInt32();
            byte count = reader.ReadByte();
            int[] sockets = ItemFieldCodec.ReadSockets(ref reader);
            ItemAttribute[] attributes = ItemFieldCodec.ReadAttributes(ref reader);

            packet = new PacketGCItemDel(window, cell, vnum, count, sockets, attributes);
            return true;
        }

        public static PacketGCItemDel Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCItemDel.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCItemDel.PacketSize, source.Length, nameof(PacketGCItemDel));
            }

            byte header = source[0];
            if (header != PacketGCItemDel.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCItemDel.PacketHeader, header, nameof(PacketGCItemDel));
            }

            if (!TryDeserialize(source, out PacketGCItemDel packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCItemDel));
            }

            return packet;
        }
    }
}
