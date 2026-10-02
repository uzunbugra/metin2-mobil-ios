using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGAttackTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:12 / client Packet.h:13: header 2;
            // struct = 1+1+4+1+1 = 8 (packet.h:564-571, Packet.h:535-542).
            Assert.AreEqual(2, PacketCGAttack.PacketHeader);
            Assert.AreEqual(8, PacketCGAttack.PacketSize);
        }

        [Test]
        public void Serialize_NormalAttack_GoldenBytes()
        {
            byte[] bytes = PacketCGAttackCodec.Serialize(new PacketCGAttack(0, 0x11223344));
            CollectionAssert.AreEqual(
                new byte[] { 0x02, 0x00, 0x44, 0x33, 0x22, 0x11, 0x00, 0x00 },
                bytes);
        }

        [Test]
        public void Serialize_SkillAttackWithCrc_GoldenBytes()
        {
            byte[] bytes = PacketCGAttackCodec.Serialize(
                new PacketCGAttack(124, 0xAABBCCDD, 0x5A, 0xC3));
            CollectionAssert.AreEqual(
                new byte[] { 0x02, 0x7C, 0xDD, 0xCC, 0xBB, 0xAA, 0x5A, 0xC3 },
                bytes);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketCGAttack(91, 777, 1, 2);
            byte[] bytes = PacketCGAttackCodec.Serialize(original);
            PacketCGAttack restored = PacketCGAttackCodec.Deserialize(bytes);

            Assert.AreEqual(original.Type, restored.Type);
            Assert.AreEqual(original.VictimVid, restored.VictimVid);
            Assert.AreEqual(original.CrcMagicCubeProcPiece, restored.CrcMagicCubeProcPiece);
            Assert.AreEqual(original.CrcMagicCubeFilePiece, restored.CrcMagicCubeFilePiece);
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(7)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGAttackCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] wrong = PacketCGAttackCodec.Serialize(new PacketCGAttack(0, 1));
            wrong[0] = 0x07;
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGAttackCodec.Deserialize(wrong);
            });
        }
    }
}
