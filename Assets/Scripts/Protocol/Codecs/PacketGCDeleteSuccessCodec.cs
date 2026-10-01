using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for the 2-byte delete-success frame (header 10 + slot index).
    /// Server: input_db.cpp:278-289. Client: UserInterface/Packet.h:1176-1180,
    /// PhaseSelect.cpp:264-276.
    /// </summary>
    public static class PacketGCDeleteSuccessCodec
    {
        public static int Serialize(in PacketGCDeleteSuccess packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCDeleteSuccess.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCDeleteSuccess.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Index);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCDeleteSuccess packet)
        {
            byte[] buffer = new byte[PacketGCDeleteSuccess.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCDeleteSuccess packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCDeleteSuccess.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCDeleteSuccess.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCDeleteSuccess.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCDeleteSuccess.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            packet = new PacketGCDeleteSuccess(reader.ReadByte());
            return true;
        }

        public static PacketGCDeleteSuccess Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCDeleteSuccess.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCDeleteSuccess.PacketSize, source.Length, nameof(PacketGCDeleteSuccess));
            }

            byte header = source[0];
            if (header != PacketGCDeleteSuccess.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCDeleteSuccess.PacketHeader, header, nameof(PacketGCDeleteSuccess));
            }

            if (!TryDeserialize(source, out PacketGCDeleteSuccess packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCDeleteSuccess));
            }

            return packet;
        }
    }
}
