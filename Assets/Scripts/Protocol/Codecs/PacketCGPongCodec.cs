using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketCGPong (1 byte, header only).
    /// Verified from Client Packet.h:1844-1847 (send site
    /// PythonNetworkStream.cpp:647-651).
    /// </summary>
    public static class PacketCGPongCodec
    {
        public static int Serialize(in PacketCGPong packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGPong.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGPong.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGPong packet)
        {
            byte[] buffer = new byte[PacketCGPong.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGPong packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGPong.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGPong.PacketSize} bytes for TPacketCGPong, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGPong.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGPong.PacketHeader:X2} (HEADER_CG_PONG), got 0x{header:X2}.";
                return false;
            }

            packet = new PacketCGPong();
            return true;
        }

        public static PacketCGPong Deserialize(ReadOnlySpan<byte> source)
        {
            if (!TryDeserialize(source, out PacketCGPong packet, out string errorMessage))
            {
                if (source.Length < PacketCGPong.PacketSize)
                {
                    throw new PacketUnderflowException(PacketCGPong.PacketSize, source.Length, nameof(PacketCGPong));
                }

                if (source[0] != PacketCGPong.PacketHeader)
                {
                    throw new InvalidPacketHeaderException(PacketCGPong.PacketHeader, source[0], nameof(PacketCGPong));
                }

                throw new PacketParseException(errorMessage, nameof(PacketCGPong));
            }

            return packet;
        }
    }
}
