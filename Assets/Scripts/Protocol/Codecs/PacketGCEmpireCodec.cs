using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCEmpire (2 bytes).
    /// References:
    /// Server: game/src/packet.h:1638-1642, input_db.cpp:157-169.
    /// Client: UserInterface/Packet.h:2083-2087, PhaseLogin.cpp:124-132.
    /// </summary>
    public static class PacketGCEmpireCodec
    {
        public static int Serialize(in PacketGCEmpire packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCEmpire.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCEmpire.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Empire);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCEmpire packet)
        {
            byte[] buffer = new byte[PacketGCEmpire.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCEmpire packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCEmpire.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCEmpire.PacketSize} bytes for TPacketGCEmpire, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCEmpire.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCEmpire.PacketHeader:X2} (HEADER_GC_EMPIRE), got 0x{header:X2}.";
                return false;
            }

            packet = new PacketGCEmpire(reader.ReadByte());
            return true;
        }

        public static PacketGCEmpire Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCEmpire.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCEmpire.PacketSize, source.Length, nameof(PacketGCEmpire));
            }

            byte header = source[0];
            if (header != PacketGCEmpire.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCEmpire.PacketHeader, header, nameof(PacketGCEmpire));
            }

            if (!TryDeserialize(source, out PacketGCEmpire packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCEmpire));
            }

            return packet;
        }
    }
}
