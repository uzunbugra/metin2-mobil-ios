using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for the dynamic CG sync-position packet (3 + 12*N bytes).
    /// Server: game/src/packet.h:597-609, input_main.cpp:1782-1818.
    /// Client: UserInterface/Packet.h:706-717.
    /// </summary>
    public static class PacketCGSyncPositionCodec
    {
        public static int SerializedSize(int elementCount)
        {
            return PacketCGSyncPosition.HeaderSize + (SyncPositionElement.FieldSize * elementCount);
        }

        public static int Serialize(in PacketCGSyncPosition packet, Span<byte> destination)
        {
            int count = packet.ElementCount;
            if (count > PacketCGSyncPosition.MaxElements)
            {
                throw new ArgumentException(
                    $"Too many sync elements: {count} exceeds server clamp {PacketCGSyncPosition.MaxElements}.",
                    nameof(packet));
            }

            int size = SerializedSize(count);
            if (destination.Length < size)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {size} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt16((ushort)size);

            byte[] slot = new byte[SyncPositionElement.FieldSize];
            for (int i = 0; i < count; i++)
            {
                SyncPositionElementCodec.Serialize(packet.Elements[i], slot);
                writer.WriteBytes(slot);
            }

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGSyncPosition packet)
        {
            int count = packet.ElementCount;
            if (count > PacketCGSyncPosition.MaxElements)
            {
                throw new ArgumentException(
                    $"Too many sync elements: {count} exceeds server clamp {PacketCGSyncPosition.MaxElements}.",
                    nameof(packet));
            }

            byte[] buffer = new byte[SerializedSize(count)];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGSyncPosition packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGSyncPosition.HeaderSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGSyncPosition.HeaderSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGSyncPosition.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGSyncPosition.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            int size = reader.ReadUInt16();
            if (size < PacketCGSyncPosition.HeaderSize || (size - PacketCGSyncPosition.HeaderSize) % SyncPositionElement.FieldSize != 0)
            {
                errorMessage = $"Invalid sync size field: {size}.";
                return false;
            }

            int count = (size - PacketCGSyncPosition.HeaderSize) / SyncPositionElement.FieldSize;
            if (count > PacketCGSyncPosition.MaxElements)
            {
                errorMessage = $"Too many sync elements: {count} exceeds {PacketCGSyncPosition.MaxElements}.";
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

            packet = new PacketCGSyncPosition(elements);
            return true;
        }

        public static PacketCGSyncPosition Deserialize(ReadOnlySpan<byte> source)
        {
            if (!TryDeserialize(source, out PacketCGSyncPosition packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGSyncPosition));
            }

            return packet;
        }
    }
}
