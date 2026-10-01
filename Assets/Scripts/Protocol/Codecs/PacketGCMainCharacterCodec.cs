using System;
using System.Text;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketGCMainCharacter2_EMPIRE (46 bytes).
    /// Server: game/src/packet.h:982-991. Client: UserInterface/Packet.h:1386-1395.
    /// </summary>
    public static class PacketGCMainCharacterCodec
    {
        public static int Serialize(in PacketGCMainCharacter packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCMainCharacter.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCMainCharacter.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.Vid);
            writer.WriteUInt16(packet.Race);
            writer.WriteFixedString(packet.Name, PacketGCMainCharacter.NameBufferLen, Encoding.ASCII);
            writer.WriteInt32(packet.X);
            writer.WriteInt32(packet.Y);
            writer.WriteInt32(packet.Z);
            writer.WriteByte(packet.Empire);
            writer.WriteByte(packet.SkillGroup);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCMainCharacter packet)
        {
            byte[] buffer = new byte[PacketGCMainCharacter.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCMainCharacter packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCMainCharacter.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCMainCharacter.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCMainCharacter.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCMainCharacter.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            uint vid = reader.ReadUInt32();
            ushort race = reader.ReadUInt16();
            string name = reader.ReadFixedString(PacketGCMainCharacter.NameBufferLen, Encoding.ASCII);
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            int z = reader.ReadInt32();
            byte empire = reader.ReadByte();
            byte skillGroup = reader.ReadByte();

            packet = new PacketGCMainCharacter(vid, race, name, x, y, z, empire, skillGroup);
            return true;
        }

        public static PacketGCMainCharacter Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCMainCharacter.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCMainCharacter.PacketSize, source.Length, nameof(PacketGCMainCharacter));
            }

            byte header = source[0];
            if (header != PacketGCMainCharacter.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCMainCharacter.PacketHeader, header, nameof(PacketGCMainCharacter));
            }

            if (!TryDeserialize(source, out PacketGCMainCharacter packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCMainCharacter));
            }

            return packet;
        }
    }
}
