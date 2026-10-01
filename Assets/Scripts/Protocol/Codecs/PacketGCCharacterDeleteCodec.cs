using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketGCCharacterDelete (5 bytes).
    /// Server: game/src/packet.h:959-963. Client: PhaseGame.cpp:273-274.
    /// </summary>
    public static class PacketGCCharacterDeleteCodec
    {
        public static int Serialize(in PacketGCCharacterDelete packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCCharacterDelete.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCCharacterDelete.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.Vid);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCCharacterDelete packet)
        {
            byte[] buffer = new byte[PacketGCCharacterDelete.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCCharacterDelete packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCCharacterDelete.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCCharacterDelete.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCCharacterDelete.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCCharacterDelete.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            packet = new PacketGCCharacterDelete(reader.ReadUInt32());
            return true;
        }

        public static PacketGCCharacterDelete Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCCharacterDelete.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCCharacterDelete.PacketSize, source.Length, nameof(PacketGCCharacterDelete));
            }

            byte header = source[0];
            if (header != PacketGCCharacterDelete.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCCharacterDelete.PacketHeader, header, nameof(PacketGCCharacterDelete));
            }

            if (!TryDeserialize(source, out PacketGCCharacterDelete packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCCharacterDelete));
            }

            return packet;
        }
    }
}
