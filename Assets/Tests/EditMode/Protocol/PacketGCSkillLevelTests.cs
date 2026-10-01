using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCSkillLevelTests
    {
        [Test]
        public void PacketGCSkillLevel_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:1036-1040, SKILL_MAX_NUM=255, entry 6B:
            // 1+6*255 = 1531.
            Assert.AreEqual(76, PacketGCSkillLevel.PacketHeader);
            Assert.AreEqual(6, PlayerSkill.FieldSize);
            Assert.AreEqual(1531, PacketGCSkillLevel.PacketSize);
        }

        [Test]
        public void PlayerSkill_RoundTrip()
        {
            var skill = new PlayerSkill { MasterType = 2, Level = 30, NextRead = 0xDEADBEEF };
            byte[] buffer = new byte[PlayerSkill.FieldSize];
            PlayerSkillCodec.Serialize(skill, buffer);

            CollectionAssert.AreEqual(
                new byte[] { 0x02, 0x1E, 0xEF, 0xBE, 0xAD, 0xDE }, buffer);

            Assert.IsTrue(PlayerSkillCodec.TryDeserialize(buffer, out PlayerSkill restored, out string _));
            Assert.IsTrue(skill.Equals(restored));
        }

        [Test]
        public void Serialize_SpotsCheckFirstSkillOffset()
        {
            var skills = new PlayerSkill[255];
            skills[0] = new PlayerSkill { MasterType = 1, Level = 20, NextRead = 1000 };
            skills[254] = new PlayerSkill { MasterType = 0, Level = 40, NextRead = 0 };
            byte[] bytes = PacketGCSkillLevelCodec.Serialize(new PacketGCSkillLevel(skills));

            Assert.AreEqual(1531, bytes.Length);
            Assert.AreEqual(0x4c, bytes[0]);
            // Skill 0 at [1..6]: master, level, nextread LE = 1000 = 0x3E8.
            Assert.AreEqual(0x01, bytes[1]);
            Assert.AreEqual(0x14, bytes[2]);
            Assert.AreEqual(0xE8, bytes[3]);
            Assert.AreEqual(0x03, bytes[4]);
            // Skill 254 at [1525..1530]: level 40 = 0x28 at [1526].
            Assert.AreEqual(0x00, bytes[1525]);
            Assert.AreEqual(0x28, bytes[1526]);
        }

        [Test]
        public void RoundTrip_PreservesAllSkills()
        {
            var original = new PlayerSkill[255];
            for (int i = 0; i < original.Length; i++)
            {
                original[i] = new PlayerSkill
                {
                    MasterType = (byte)(i % 4),
                    Level = (byte)(i % 41),
                    NextRead = (uint)(i * 7919)
                };
            }

            PacketGCSkillLevel restored =
                PacketGCSkillLevelCodec.Deserialize(PacketGCSkillLevelCodec.Serialize(new PacketGCSkillLevel(original)));

            for (int i = 0; i < original.Length; i++)
            {
                Assert.IsTrue(original[i].Equals(restored.Skills[i]), $"Skill {i} mismatch.");
            }
        }

        [TestCase(0)]
        [TestCase(1530)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x4c;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCSkillLevelCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[1531];
            badHeader[0] = 0x10;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCSkillLevelCodec.Deserialize(badHeader);
            });
        }
    }
}
