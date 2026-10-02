using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCStunTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:134 / client Packet.h:155: header 13;
            // struct = 1+4 = 5 (packet.h:1051-1055, Packet.h:1363-1367).
            Assert.AreEqual(13, PacketGCStun.PacketHeader);
            Assert.AreEqual(5, PacketGCStun.PacketSize);
        }

        [Test]
        public void Serialize_GoldenBytes()
        {
            byte[] bytes = PacketGCStunCodec.Serialize(new PacketGCStun(7));
            CollectionAssert.AreEqual(new byte[] { 0x0D, 0x07, 0x00, 0x00, 0x00 }, bytes);
        }

        [Test]
        public void RoundTrip_PreservesVid()
        {
            PacketGCStun restored = PacketGCStunCodec.Deserialize(
                PacketGCStunCodec.Serialize(new PacketGCStun(0xCAFEBABE)));
            Assert.AreEqual(0xCAFEBABEu, restored.Vid);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCStunCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCStunCodec.Deserialize(new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x00 });
            });
        }
    }
}
