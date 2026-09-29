using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCPhase (2 bytes).
    /// Verified from Server packet.h:814-818 and desc.cpp:522-525.
    /// </summary>
    public static class PacketGCPhaseCodec
    {
        public static int Serialize(in PacketGCPhase packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCPhase.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCPhase.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte((byte)packet.Phase);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCPhase packet)
        {
            byte[] buffer = new byte[PacketGCPhase.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCPhase packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCPhase.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCPhase.PacketSize} bytes for TPacketGCPhase, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCPhase.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCPhase.PacketHeader:X2} (HEADER_GC_PHASE), got 0x{header:X2}.";
                return false;
            }

            byte phaseByte = reader.ReadByte();
            packet = new PacketGCPhase((PhaseType)phaseByte);
            return true;
        }

        public static PacketGCPhase Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCPhase.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCPhase.PacketSize, source.Length, nameof(PacketGCPhase));
            }

            byte header = source[0];
            if (header != PacketGCPhase.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCPhase.PacketHeader, header, nameof(PacketGCPhase));
            }

            if (!TryDeserialize(source, out PacketGCPhase packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCPhase));
            }

            return packet;
        }
    }
}
