using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketGCSkillLevel (1531 bytes).
    /// Server: game/src/packet.h:1036-1040, char_skill.cpp:158-168.
    /// Client: UserInterface/Packet.h:1977-1981.
    /// </summary>
    public static class PacketGCSkillLevelCodec
    {
        public static int Serialize(in PacketGCSkillLevel packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCSkillLevel.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCSkillLevel.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);

            PlayerSkill[] skills = packet.Skills ?? new PlayerSkill[PacketGCSkillLevel.SkillCount];
            byte[] slotBuffer = new byte[PlayerSkill.FieldSize];
            for (int i = 0; i < PacketGCSkillLevel.SkillCount; i++)
            {
                PlayerSkill slot = i < skills.Length ? skills[i] : default;
                PlayerSkillCodec.Serialize(slot, slotBuffer);
                writer.WriteBytes(slotBuffer);
            }

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCSkillLevel packet)
        {
            byte[] buffer = new byte[PacketGCSkillLevel.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCSkillLevel packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCSkillLevel.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCSkillLevel.PacketSize} bytes for TPacketGCSkillLevel, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCSkillLevel.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCSkillLevel.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            var skills = new PlayerSkill[PacketGCSkillLevel.SkillCount];
            for (int i = 0; i < skills.Length; i++)
            {
                if (!PlayerSkillCodec.TryDeserialize(reader.ReadSpan(PlayerSkill.FieldSize), out PlayerSkill slot, out string slotError))
                {
                    errorMessage = $"Skill {i}: {slotError}";
                    return false;
                }

                skills[i] = slot;
            }

            packet = new PacketGCSkillLevel(skills);
            return true;
        }

        public static PacketGCSkillLevel Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCSkillLevel.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCSkillLevel.PacketSize, source.Length, nameof(PacketGCSkillLevel));
            }

            byte header = source[0];
            if (header != PacketGCSkillLevel.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCSkillLevel.PacketHeader, header, nameof(PacketGCSkillLevel));
            }

            if (!TryDeserialize(source, out PacketGCSkillLevel packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCSkillLevel));
            }

            return packet;
        }
    }
}
