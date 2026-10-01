using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketGCItemUpdate (38 bytes, header 25).
    /// Server: game/src/packet.h:1108-1115, item.cpp:215-217.
    /// Client: UserInterface/Packet.h:1703-1710, PhaseGameItem.cpp:278-294.
    /// </summary>
    public static class PacketGCItemUpdateCodec
    {
        public static int Serialize(in PacketGCItemUpdate packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCItemUpdate.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCItemUpdate.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            ItemFieldCodec.WriteCell(ref writer, packet.Window, packet.Cell);
            writer.WriteByte(packet.Count);
            ItemFieldCodec.WriteSockets(ref writer, packet.Sockets);
            ItemFieldCodec.WriteAttributes(ref writer, packet.Attributes);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCItemUpdate packet)
        {
            byte[] buffer = new byte[PacketGCItemUpdate.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCItemUpdate packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCItemUpdate.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCItemUpdate.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCItemUpdate.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCItemUpdate.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            ItemFieldCodec.ReadCell(ref reader, out byte window, out ushort cell);
            byte count = reader.ReadByte();
            int[] sockets = ItemFieldCodec.ReadSockets(ref reader);
            ItemAttribute[] attributes = ItemFieldCodec.ReadAttributes(ref reader);

            packet = new PacketGCItemUpdate(window, cell, count, sockets, attributes);
            return true;
        }

        public static PacketGCItemUpdate Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCItemUpdate.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCItemUpdate.PacketSize, source.Length, nameof(PacketGCItemUpdate));
            }

            byte header = source[0];
            if (header != PacketGCItemUpdate.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCItemUpdate.PacketHeader, header, nameof(PacketGCItemUpdate));
            }

            if (!TryDeserialize(source, out PacketGCItemUpdate packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCItemUpdate));
            }

            return packet;
        }
    }
}
