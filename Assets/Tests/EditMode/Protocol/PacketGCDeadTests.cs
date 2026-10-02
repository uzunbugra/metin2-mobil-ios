using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCDeadTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:135 / client Packet.h:156: header 14;
            // struct = 1+4 = 5 (packet.h:1057-1061, Packet.h:1369-1373).
            Assert.AreEqual(14, PacketGCDead.PacketHeader);
            Assert.AreEqual(5, PacketGCDead.PacketSize);
        }

        [Test]
        public void Serialize_GoldenBytes()
        {
            byte[] bytes = PacketGCDeadCodec.Serialize(new PacketGCDead(1234));
            CollectionAssert.AreEqual(new byte[] { 0x0E, 0xD2, 0x04, 0x00, 0x00 }, bytes);
        }

        [Test]
        public void RoundTrip_PreservesVid()
        {
            PacketGCDead restored = PacketGCDeadCodec.Deserialize(
                PacketGCDeadCodec.Serialize(new PacketGCDead(987654321)));
            Assert.AreEqual(987654321u, restored.Vid);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCDeadCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCDeadCodec.Deserialize(new byte[] { 0x0D, 0x00, 0x00, 0x00, 0x00 });
            });
        }
    }
}
