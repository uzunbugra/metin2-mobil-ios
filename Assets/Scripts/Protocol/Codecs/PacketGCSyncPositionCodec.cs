using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for the dynamic GC sync-position packet (3 + 12*N bytes).
    /// Server: game/src/packet.h:1310-1322. Client: UserInterface/Packet.h:1947-1953.
    /// </summary>
    public static class PacketGCSyncPositionCodec
    {
        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCSyncPosition packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCSyncPosition.HeaderSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCSyncPosition.HeaderSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCSyncPosition.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCSyncPosition.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            int size = reader.ReadUInt16();
            if (size < PacketGCSyncPosition.HeaderSize || (size - PacketGCSyncPosition.HeaderSize) % SyncPositionElement.FieldSize != 0)
            {
                errorMessage = $"Invalid sync size field: {size}.";
                return false;
            }

            int count = (size - PacketGCSyncPosition.HeaderSize) / SyncPositionElement.FieldSize;
            if (count > PacketGCSyncPosition.MaxElements)
            {
                errorMessage = $"Too many sync elements: {count} exceeds {PacketGCSyncPosition.MaxElements}.";
                return false;
            }

            if (source.Length < size)
            {
                errorMessage = $"Buffer truncated: declared {size} bytes, got {source.Length}.";
                return false;
            }

            var elements = new SyncPositionElement[count];
            for (int i = 0; i < count; i++)
            {
                if (!SyncPositionElementCodec.TryDeserialize(
                    reader.ReadSpan(SyncPositionElement.FieldSize), out SyncPositionElement element, out string elementError))
                {
                    errorMessage = $"Element {i}: {elementError}";
                    return false;
                }

                elements[i] = element;
            }

            packet = new PacketGCSyncPosition(elements);
            return true;
        }

        public static PacketGCSyncPosition Deserialize(ReadOnlySpan<byte> source)
        {
            if (!TryDeserialize(source, out PacketGCSyncPosition packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCSyncPosition));
            }

            return packet;
        }

        public static byte[] Serialize(in PacketGCSyncPosition packet)
        {
            var elements = packet.Elements ?? new SyncPositionElement[0];
            byte[] buffer = new byte[PacketGCSyncPosition.HeaderSize + (SyncPositionElement.FieldSize * elements.Length)];
            var writer = new PacketWriter(buffer);
            writer.WriteByte(packet.Header);
            writer.WriteUInt16((ushort)buffer.Length);

            byte[] slot = new byte[SyncPositionElement.FieldSize];
            foreach (SyncPositionElement element in elements)
            {
                SyncPositionElementCodec.Serialize(element, slot);
                writer.WriteBytes(slot);
            }

            return buffer;
        }
    }
}
