using System;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for the 1-byte delete-failure frame (header 11 only).
    /// Server: input_db.cpp:291-300. Client: PhaseSelect.cpp:278-286
    /// (1-byte TPacketGCBlank).
    /// </summary>
    public static class PacketGCDeleteFailureCodec
    {
        public static int Serialize(in PacketGCDeleteFailure packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCDeleteFailure.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCDeleteFailure.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            destination[0] = packet.Header;
            return PacketGCDeleteFailure.PacketSize;
        }

        public static byte[] Serialize(in PacketGCDeleteFailure packet)
        {
            return new byte[] { packet.Header };
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCDeleteFailure packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCDeleteFailure.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCDeleteFailure.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            if (source[0] != PacketGCDeleteFailure.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCDeleteFailure.PacketHeader:X2}, got 0x{source[0]:X2}.";
                return false;
            }

            packet = new PacketGCDeleteFailure();
            return true;
        }

        public static PacketGCDeleteFailure Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCDeleteFailure.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCDeleteFailure.PacketSize, source.Length, nameof(PacketGCDeleteFailure));
            }

            if (source[0] != PacketGCDeleteFailure.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCDeleteFailure.PacketHeader, source[0], nameof(PacketGCDeleteFailure));
            }

            return new PacketGCDeleteFailure();
        }
    }
}
