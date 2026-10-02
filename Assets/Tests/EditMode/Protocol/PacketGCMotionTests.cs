using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCMotionTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:161: header 36;
            // struct = 1+4+4+2 = 11 (packet.h:1158-1164, Packet.h:1617-1623).
            Assert.AreEqual(36, PacketGCMotion.PacketHeader);
            Assert.AreEqual(11, PacketGCMotion.PacketSize);
        }

        [Test]
        public void Serialize_GoldenBytes()
        {
            byte[] bytes = PacketGCMotionCodec.Serialize(new PacketGCMotion(1, 2, 3));
            CollectionAssert.AreEqual(
                new byte[] { 0x24, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00 },
                bytes);
        }

        [Test]
        public void Serialize_VictimlessMotion_ZeroVictimVid()
        {
            byte[] bytes = PacketGCMotionCodec.Serialize(new PacketGCMotion(5, 0, 7));
            CollectionAssert.AreEqual(
                new byte[] { 0x24, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0x00 },
                bytes);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketGCMotion(0x11111111, 0x22222222, 0xABCD);
            PacketGCMotion restored = PacketGCMotionCodec.Deserialize(
                PacketGCMotionCodec.Serialize(original));

            Assert.AreEqual(original.Vid, restored.Vid);
            Assert.AreEqual(original.VictimVid, restored.VictimVid);
            Assert.AreEqual(original.Motion, restored.Motion);
        }

        [TestCase(0)]
        [TestCase(6)]
        [TestCase(10)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCMotionCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] wrong = PacketGCMotionCodec.Serialize(new PacketGCMotion(1, 2, 3));
            wrong[0] = 0x42;
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCMotionCodec.Deserialize(wrong);
            });
        }
    }
}
