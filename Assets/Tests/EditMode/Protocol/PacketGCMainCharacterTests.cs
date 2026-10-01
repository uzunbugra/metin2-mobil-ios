using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCMainCharacterTests
    {
        [Test]
        public void PacketGCMainCharacter_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:982-991 (header packet.h:225):
            // 1+4+2+25+12+1+1 = 46.
            Assert.AreEqual(113, PacketGCMainCharacter.PacketHeader);
            Assert.AreEqual(46, PacketGCMainCharacter.PacketSize);
        }

        public static PacketGCMainCharacter SamplePacket()
        {
            return new PacketGCMainCharacter(
                0x00A1B2C3, 7, "HeroName", 474387, 954234, 0, 2, 1);
        }

        [Test]
        public void Serialize_ProducesExactWireLayout()
        {
            byte[] bytes = PacketGCMainCharacterCodec.Serialize(SamplePacket());

            Assert.AreEqual(46, bytes.Length);
            Assert.AreEqual(0x71, bytes[0]);
            // dwVID LE at [1..4] = 0x00A1B2C3.
            Assert.AreEqual(0xC3, bytes[1]);
            Assert.AreEqual(0xB2, bytes[2]);
            Assert.AreEqual(0xA1, bytes[3]);
            Assert.AreEqual(0x00, bytes[4]);
            // wRaceNum LE = 7 at [5..6].
            Assert.AreEqual(0x07, bytes[5]);
            Assert.AreEqual(0x00, bytes[6]);
            // Name AFTER race (struct order!): "HeroName" at [7..14].
            Assert.AreEqual((byte)'H', bytes[7]);
            Assert.AreEqual((byte)'e', bytes[14]);
            Assert.AreEqual(0x00, bytes[15]);
            // lx LE = 474387 = 0x73D13 at [32..35].
            Assert.AreEqual(0x13, bytes[32]);
            Assert.AreEqual(0x3D, bytes[33]);
            Assert.AreEqual(0x07, bytes[34]);
            Assert.AreEqual(0x00, bytes[35]);
            // empire/skill at [44..45].
            Assert.AreEqual(0x02, bytes[44]);
            Assert.AreEqual(0x01, bytes[45]);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            PacketGCMainCharacter original = SamplePacket();
            PacketGCMainCharacter restored =
                PacketGCMainCharacterCodec.Deserialize(PacketGCMainCharacterCodec.Serialize(original));

            Assert.AreEqual(original.Vid, restored.Vid);
            Assert.AreEqual(original.Race, restored.Race);
            Assert.AreEqual(original.Name, restored.Name);
            Assert.AreEqual(original.X, restored.X);
            Assert.AreEqual(original.Y, restored.Y);
            Assert.AreEqual(original.Z, restored.Z);
            Assert.AreEqual(original.Empire, restored.Empire);
            Assert.AreEqual(original.SkillGroup, restored.SkillGroup);
        }

        [TestCase(0)]
        [TestCase(45)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x71;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCMainCharacterCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_Legacy45ByteHeader_ThrowsInvalidPacketHeaderException()
        {
            // Header 15 is the legacy 45B layout (no empire) — must not decode
            // as the 46B empire layout. Length check runs first, so feed a
            // full-size buffer to reach the header check.
            byte[] legacy = new byte[46];
            legacy[0] = 0x0f;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCMainCharacterCodec.Deserialize(legacy);
            });
        }
    }
}
