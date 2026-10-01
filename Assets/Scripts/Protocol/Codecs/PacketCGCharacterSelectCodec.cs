using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for command_player_select (2 bytes).
    /// Server: game/src/packet.h:530-534. Client: UserInterface/Packet.h:529-533.
    /// </summary>
    public static class PacketCGCharacterSelectCodec
    {
        public static int Serialize(in PacketCGCharacterSelect packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGCharacterSelect.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGCharacterSelect.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Index);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGCharacterSelect packet)
        {
            byte[] buffer = new byte[PacketCGCharacterSelect.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGCharacterSelect packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGCharacterSelect.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGCharacterSelect.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGCharacterSelect.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGCharacterSelect.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            packet = new PacketCGCharacterSelect(reader.ReadByte());
            return true;
        }

        public static PacketCGCharacterSelect Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGCharacterSelect.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGCharacterSelect.PacketSize, source.Length, nameof(PacketCGCharacterSelect));
            }

            byte header = source[0];
            if (header != PacketCGCharacterSelect.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGCharacterSelect.PacketHeader, header, nameof(PacketCGCharacterSelect));
            }

            if (!TryDeserialize(source, out PacketCGCharacterSelect packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGCharacterSelect));
            }

            return packet;
        }
    }
}
