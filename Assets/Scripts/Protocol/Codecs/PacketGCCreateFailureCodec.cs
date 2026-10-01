using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketGCCreateFailure (2 bytes).
    /// Server: game/src/packet.h:862-866, input_login.cpp:427-482.
    /// Client: UserInterface/Packet.h:1163-1167, PhaseSelect.cpp:252-262.
    /// </summary>
    public static class PacketGCCreateFailureCodec
    {
        public static int Serialize(in PacketGCCreateFailure packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCCreateFailure.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCCreateFailure.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Type);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCCreateFailure packet)
        {
            byte[] buffer = new byte[PacketGCCreateFailure.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCCreateFailure packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCCreateFailure.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCCreateFailure.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCCreateFailure.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCCreateFailure.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            packet = new PacketGCCreateFailure(reader.ReadByte());
            return true;
        }

        public static PacketGCCreateFailure Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCCreateFailure.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCCreateFailure.PacketSize, source.Length, nameof(PacketGCCreateFailure));
            }

            byte header = source[0];
            if (header != PacketGCCreateFailure.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCCreateFailure.PacketHeader, header, nameof(PacketGCCreateFailure));
            }

            if (!TryDeserialize(source, out PacketGCCreateFailure packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCCreateFailure));
            }

            return packet;
        }
    }
}
