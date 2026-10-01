using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Field codec for one 12-byte sync-position element (both directions).
    /// </summary>
    public static class SyncPositionElementCodec
    {
        public static int Serialize(in SyncPositionElement element, Span<byte> destination)
        {
            if (destination.Length < SyncPositionElement.FieldSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {SyncPositionElement.FieldSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteUInt32(element.Vid);
            writer.WriteInt32(element.X);
            writer.WriteInt32(element.Y);

            return writer.BytesWritten;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out SyncPositionElement element, out string errorMessage)
        {
            element = default;
            errorMessage = null;

            if (source.Length < SyncPositionElement.FieldSize)
            {
                errorMessage = $"Buffer truncated: expected at least {SyncPositionElement.FieldSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            element = new SyncPositionElement
            {
                Vid = reader.ReadUInt32(),
                X = reader.ReadInt32(),
                Y = reader.ReadInt32()
            };
            return true;
        }

        public static SyncPositionElement Deserialize(ReadOnlySpan<byte> source)
        {
            if (!TryDeserialize(source, out SyncPositionElement element, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(SyncPositionElement));
            }

            return element;
        }
    }
}
