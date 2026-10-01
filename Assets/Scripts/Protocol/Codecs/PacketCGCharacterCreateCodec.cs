using System;
using System.Text;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for command_player_create (34 bytes).
    /// Server: game/src/packet.h:543-554. Client: UserInterface/Packet.h:1143-1154.
    /// </summary>
    public static class PacketCGCharacterCreateCodec
    {
        public static int Serialize(in PacketCGCharacterCreate packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGCharacterCreate.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGCharacterCreate.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Index);
            writer.WriteFixedString(packet.Name, PacketCGCharacterCreate.NameBufferLen, Encoding.ASCII);
            writer.WriteUInt16(packet.Job);
            writer.WriteByte(packet.Shape);
            writer.WriteByte(packet.Con);
            writer.WriteByte(packet.Int);
            writer.WriteByte(packet.Str);
            writer.WriteByte(packet.Dex);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGCharacterCreate packet)
        {
            byte[] buffer = new byte[PacketCGCharacterCreate.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGCharacterCreate packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGCharacterCreate.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGCharacterCreate.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGCharacterCreate.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGCharacterCreate.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            byte index = reader.ReadByte();
            string name = reader.ReadFixedString(PacketCGCharacterCreate.NameBufferLen, Encoding.ASCII);
            ushort job = reader.ReadUInt16();
            byte shape = reader.ReadByte();
            byte con = reader.ReadByte();
            byte @int = reader.ReadByte();
            byte str = reader.ReadByte();
            byte dex = reader.ReadByte();

            packet = new PacketCGCharacterCreate(index, name, job, shape, con, @int, str, dex);
            return true;
        }

        public static PacketCGCharacterCreate Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGCharacterCreate.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGCharacterCreate.PacketSize, source.Length, nameof(PacketCGCharacterCreate));
            }

            byte header = source[0];
            if (header != PacketCGCharacterCreate.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGCharacterCreate.PacketHeader, header, nameof(PacketCGCharacterCreate));
            }

            if (!TryDeserialize(source, out PacketCGCharacterCreate packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGCharacterCreate));
            }

            return packet;
        }
    }
}
