using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCDamageInfoTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:258: header 135;
            // struct = 1+4+1+4 = 10 (packet.h:2093-2099).
            Assert.AreEqual(135, PacketGCDamageInfo.PacketHeader);
            Assert.AreEqual(10, PacketGCDamageInfo.PacketSize);
        }

        [Test]
        public void Serialize_GoldenBytes()
        {
            byte[] bytes = PacketGCDamageInfoCodec.Serialize(new PacketGCDamageInfo(9, 1, 250));
            CollectionAssert.AreEqual(
                new byte[] { 0x87, 0x09, 0x00, 0x00, 0x00, 0x01, 0xFA, 0x00, 0x00, 0x00 },
                bytes);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketGCDamageInfo(0xABCDEF01, 3, -75);
            PacketGCDamageInfo restored = PacketGCDamageInfoCodec.Deserialize(
                PacketGCDamageInfoCodec.Serialize(original));

            Assert.AreEqual(original.Vid, restored.Vid);
            Assert.AreEqual(original.Flag, restored.Flag);
            Assert.AreEqual(original.Damage, restored.Damage);
        }

        [TestCase(0)]
        [TestCase(5)]
        [TestCase(9)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCDamageInfoCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] wrong = PacketGCDamageInfoCodec.Serialize(new PacketGCDamageInfo(1, 0, 0));
            wrong[0] = 0x42;
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCDamageInfoCodec.Deserialize(wrong);
            });
        }
    }
}
