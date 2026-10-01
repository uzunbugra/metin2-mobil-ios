using System;
using System.Text;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for command_player_delete (10 bytes).
    /// Server: game/src/packet.h:536-541. Client: UserInterface/Packet.h:1169-1174.
    /// </summary>
    public static class PacketCGCharacterDeleteCodec
    {
        public static int Serialize(in PacketCGCharacterDelete packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGCharacterDelete.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGCharacterDelete.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Index);
            writer.WriteFixedString(packet.PrivateCode, PacketCGCharacterDelete.PrivateCodeBufferLen, Encoding.ASCII);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGCharacterDelete packet)
        {
            byte[] buffer = new byte[PacketCGCharacterDelete.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGCharacterDelete packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGCharacterDelete.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGCharacterDelete.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGCharacterDelete.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGCharacterDelete.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            byte index = reader.ReadByte();
            string code = reader.ReadFixedString(PacketCGCharacterDelete.PrivateCodeBufferLen, Encoding.ASCII);

            packet = new PacketCGCharacterDelete(index, code);
            return true;
        }

        public static PacketCGCharacterDelete Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGCharacterDelete.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGCharacterDelete.PacketSize, source.Length, nameof(PacketCGCharacterDelete));
            }

            byte header = source[0];
            if (header != PacketCGCharacterDelete.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGCharacterDelete.PacketHeader, header, nameof(PacketCGCharacterDelete));
            }

            if (!TryDeserialize(source, out PacketCGCharacterDelete packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGCharacterDelete));
            }

            return packet;
        }
    }
}
