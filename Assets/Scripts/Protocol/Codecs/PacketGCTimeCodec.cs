using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketGCTime (5 bytes).
    /// Server: game/src/packet.h:1891-1895. Client: UserInterface/Packet.h:2240-2244.
    /// </summary>
    public static class PacketGCTimeCodec
    {
        public static int Serialize(in PacketGCTime packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCTime.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCTime.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.Time);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCTime packet)
        {
            byte[] buffer = new byte[PacketGCTime.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCTime packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCTime.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCTime.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCTime.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCTime.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            packet = new PacketGCTime(reader.ReadUInt32());
            return true;
        }

        public static PacketGCTime Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCTime.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCTime.PacketSize, source.Length, nameof(PacketGCTime));
            }

            byte header = source[0];
            if (header != PacketGCTime.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCTime.PacketHeader, header, nameof(PacketGCTime));
            }

            if (!TryDeserialize(source, out PacketGCTime packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCTime));
            }

            return packet;
        }
    }
}
