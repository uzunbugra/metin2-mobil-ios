using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for server TPacketGCItemSet (51 bytes, header 21).
    /// Server: game/src/packet.h:1073-1084, char_item.cpp:408-425.
    /// Client: UserInterface/Packet.h:1673-1684, PhaseGameItem.cpp:238-264.
    /// </summary>
    public static class PacketGCItemSetCodec
    {
        public static int Serialize(in PacketGCItemSet packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCItemSet.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCItemSet.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            ItemFieldCodec.WriteCell(ref writer, packet.Window, packet.Cell);
            writer.WriteUInt32(packet.Vnum);
            writer.WriteByte(packet.Count);
            writer.WriteUInt32(packet.Flags);
            writer.WriteUInt32(packet.AntiFlags);
            writer.WriteByte(packet.Highlight ? (byte)1 : (byte)0);
            ItemFieldCodec.WriteSockets(ref writer, packet.Sockets);
            ItemFieldCodec.WriteAttributes(ref writer, packet.Attributes);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCItemSet packet)
        {
            byte[] buffer = new byte[PacketGCItemSet.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCItemSet packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCItemSet.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCItemSet.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCItemSet.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCItemSet.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            ItemFieldCodec.ReadCell(ref reader, out byte window, out ushort cell);
            uint vnum = reader.ReadUInt32();
            byte count = reader.ReadByte();
            uint flags = reader.ReadUInt32();
            uint antiFlags = reader.ReadUInt32();
            bool highlight = reader.ReadByte() != 0;
            int[] sockets = ItemFieldCodec.ReadSockets(ref reader);
            ItemAttribute[] attributes = ItemFieldCodec.ReadAttributes(ref reader);

            packet = new PacketGCItemSet(window, cell, vnum, count, flags, antiFlags, highlight, sockets, attributes);
            return true;
        }

        public static PacketGCItemSet Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCItemSet.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCItemSet.PacketSize, source.Length, nameof(PacketGCItemSet));
            }

            byte header = source[0];
            if (header != PacketGCItemSet.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCItemSet.PacketHeader, header, nameof(PacketGCItemSet));
            }

            if (!TryDeserialize(source, out PacketGCItemSet packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCItemSet));
            }

            return packet;
        }
    }
}
