using System;
using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Skill table (TPacketGCSkillLevel, 1531 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:1036-1040` (`TPlayerSkill skills[SKILL_MAX_NUM]`,
    /// `SKILL_MAX_NUM=255`, `common/length.h:48`), sent `char_skill.cpp:158-168`
    /// (`SkillLevelPacket`, header `HEADER_GC_SKILL_LEVEL=76`);
    /// Client `UserInterface/Packet.h:1977-1981` (`TPacketGCSkillLevelNew`,
    /// `SKILL_MAX_NUM 255`, `Packet.h:1962`), header
    /// `HEADER_GC_SKILL_LEVEL_NEW=76` (`Packet.h:212`).
    /// Wire layout (total 1531 = 1+6*255):
    /// [0]        BYTE bHeader (76 = 0x4c)
    /// [1..1530] TPlayerSkill skills[255] (6 bytes each)
    /// </summary>
    public struct PacketGCSkillLevel : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_SKILL_LEVEL;
        public const int SkillCount = 255; // SKILL_MAX_NUM
        public const int PacketSize = 1 + (PlayerSkill.FieldSize * SkillCount); // 1531 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public PlayerSkill[] Skills { get; set; }

        public PacketGCSkillLevel(PlayerSkill[] skills)
        {
            Skills = Pad(skills);
        }

        private static PlayerSkill[] Pad(PlayerSkill[] source)
        {
            var result = new PlayerSkill[SkillCount];
            if (source != null)
            {
                Array.Copy(source, 0, result, 0, Math.Min(source.Length, SkillCount));
            }

            return result;
        }

        public override string ToString()
        {
            return $"PacketGCSkillLevel(Count={SkillCount})";
        }
    }
}
