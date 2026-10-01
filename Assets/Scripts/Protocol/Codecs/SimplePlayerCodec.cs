using System;
using System.Text;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Field codec for TSimplePlayer (63 bytes, #pragma pack(1)).
    /// References:
    /// Server: common/tables.h:275-291. Client: UserInterface/Packet.h:1096-1113.
    /// long/LONG fields are 4-byte little-endian integers on both sides
    /// (Win32/x86 ABI the game ships with).
    /// </summary>
    public static class SimplePlayerCodec
    {
        public static int Serialize(in SimplePlayer player, Span<byte> destination)
        {
            if (destination.Length < SimplePlayer.FieldSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {SimplePlayer.FieldSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteUInt32(player.Id);
            writer.WriteFixedString(player.Name, SimplePlayer.NameBufferLen, Encoding.ASCII);
            writer.WriteByte(player.Job);
            writer.WriteByte(player.Level);
            writer.WriteUInt32(player.PlayMinutes);
            writer.WriteByte(player.St);
            writer.WriteByte(player.Ht);
            writer.WriteByte(player.Dx);
            writer.WriteByte(player.Iq);
            writer.WriteUInt16(player.MainPart);
            writer.WriteByte(player.ChangeName);
            writer.WriteUInt16(player.HairPart);
            writer.WriteZeros(4); // bDummy[4]
            writer.WriteInt32(player.X);
            writer.WriteInt32(player.Y);
            writer.WriteUInt32(player.AddrNetworkOrder);
            writer.WriteUInt16(player.Port);
            writer.WriteByte(player.SkillGroup);

            return writer.BytesWritten;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out SimplePlayer player, out string errorMessage)
        {
            player = default;
            errorMessage = null;

            if (source.Length < SimplePlayer.FieldSize)
            {
                errorMessage = $"Buffer truncated: expected at least {SimplePlayer.FieldSize} bytes for TSimplePlayer, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            var parsed = new SimplePlayer
            {
                Id = reader.ReadUInt32()
            };
            parsed.Name = reader.ReadFixedString(SimplePlayer.NameBufferLen, Encoding.ASCII);
            parsed.Job = reader.ReadByte();
            parsed.Level = reader.ReadByte();
            parsed.PlayMinutes = reader.ReadUInt32();
            parsed.St = reader.ReadByte();
            parsed.Ht = reader.ReadByte();
            parsed.Dx = reader.ReadByte();
            parsed.Iq = reader.ReadByte();
            parsed.MainPart = reader.ReadUInt16();
            parsed.ChangeName = reader.ReadByte();
            parsed.HairPart = reader.ReadUInt16();
            reader.ReadSpan(4); // bDummy[4]
            parsed.X = reader.ReadInt32();
            parsed.Y = reader.ReadInt32();
            parsed.AddrNetworkOrder = reader.ReadUInt32();
            parsed.Port = reader.ReadUInt16();
            parsed.SkillGroup = reader.ReadByte();

            player = parsed;
            return true;
        }

        public static SimplePlayer Deserialize(ReadOnlySpan<byte> source)
        {
            if (!TryDeserialize(source, out SimplePlayer player, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(SimplePlayer));
            }

            return player;
        }
    }
}
