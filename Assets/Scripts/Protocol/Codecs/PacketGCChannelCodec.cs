using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketGCChannel (2 bytes).
    /// Server: game/src/packet.h:1995-1999. Client: UserInterface/Packet.h:2377-2381.
    /// </summary>
    public static class PacketGCChannelCodec
    {
        public static int Serialize(in PacketGCChannel packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCChannel.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCChannel.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Channel);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCChannel packet)
        {
            byte[] buffer = new byte[PacketGCChannel.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCChannel packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCChannel.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCChannel.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCChannel.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCChannel.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            packet = new PacketGCChannel(reader.ReadByte());
            return true;
        }

        public static PacketGCChannel Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCChannel.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCChannel.PacketSize, source.Length, nameof(PacketGCChannel));
            }

            byte header = source[0];
            if (header != PacketGCChannel.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCChannel.PacketHeader, header, nameof(PacketGCChannel));
            }

            if (!TryDeserialize(source, out PacketGCChannel packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCChannel));
            }

            return packet;
        }
    }
}
