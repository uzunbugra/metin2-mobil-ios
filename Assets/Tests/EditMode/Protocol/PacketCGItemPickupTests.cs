using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGItemPickupTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:24 / client Packet.h:25: header 15;
            // struct = 1 + 4 = 5 (packet.h:668-672).
            Assert.AreEqual(15, PacketCGItemPickup.PacketHeader);
            Assert.AreEqual(5, PacketCGItemPickup.PacketSize);
        }

        [Test]
        public void Serialize_GoldenBytes()
        {
            byte[] bytes = PacketCGItemPickupCodec.Serialize(new PacketCGItemPickup(42));
            CollectionAssert.AreEqual(new byte[] { 0x0F, 0x2A, 0x00, 0x00, 0x00 }, bytes);
        }

        [Test]
        public void RoundTrip_PreservesVid()
        {
            PacketCGItemPickup restored = PacketCGItemPickupCodec.Deserialize(
                PacketCGItemPickupCodec.Serialize(new PacketCGItemPickup(0xCAFEBABE)));
            Assert.AreEqual(0xCAFEBABEu, restored.Vid);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGItemPickupCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGItemPickupCodec.Deserialize(new byte[] { 0x0B, 0x00, 0x00, 0x00, 0x00 });
            });
        }
    }
}
