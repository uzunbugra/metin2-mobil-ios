using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCPing (1 byte, header only).
    /// Verified from Server packet.h:1259-1262 (client Packet.h:1839-1842).
    /// </summary>
    public static class PacketGCPingCodec
    {
        public static int Serialize(in PacketGCPing packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCPing.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCPing.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCPing packet)
        {
            byte[] buffer = new byte[PacketGCPing.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCPing packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCPing.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCPing.PacketSize} bytes for TPacketGCPing, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCPing.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCPing.PacketHeader:X2} (HEADER_GC_PING), got 0x{header:X2}.";
                return false;
            }

            packet = new PacketGCPing();
            return true;
        }

        public static PacketGCPing Deserialize(ReadOnlySpan<byte> source)
        {
            if (!TryDeserialize(source, out PacketGCPing packet, out string errorMessage))
            {
                if (source.Length < PacketGCPing.PacketSize)
                {
                    throw new PacketUnderflowException(PacketGCPing.PacketSize, source.Length, nameof(PacketGCPing));
                }

                if (source[0] != PacketGCPing.PacketHeader)
                {
                    throw new InvalidPacketHeaderException(PacketGCPing.PacketHeader, source[0], nameof(PacketGCPing));
                }

                throw new PacketParseException(errorMessage, nameof(PacketGCPing));
            }

            return packet;
        }
    }
}
