using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Field codec for TPlayerSkill (6 bytes).
    /// Server: common/tables.h:337-342. Client: UserInterface/Packet.h:1970-1975.
    /// </summary>
    public static class PlayerSkillCodec
    {
        public static int Serialize(in PlayerSkill skill, Span<byte> destination)
        {
            if (destination.Length < PlayerSkill.FieldSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PlayerSkill.FieldSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(skill.MasterType);
            writer.WriteByte(skill.Level);
            writer.WriteUInt32(skill.NextRead);

            return writer.BytesWritten;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PlayerSkill skill, out string errorMessage)
        {
            skill = default;
            errorMessage = null;

            if (source.Length < PlayerSkill.FieldSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PlayerSkill.FieldSize} bytes for TPlayerSkill, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            skill = new PlayerSkill
            {
                MasterType = reader.ReadByte(),
                Level = reader.ReadByte(),
                NextRead = reader.ReadUInt32()
            };
            return true;
        }

        public static PlayerSkill Deserialize(ReadOnlySpan<byte> source)
        {
            if (!TryDeserialize(source, out PlayerSkill skill, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PlayerSkill));
            }

            return skill;
        }
    }
}
