using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGItemUseTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:21 / client Packet.h:22: header 11;
            // struct = 1 + TItemPos(3) = 4 (packet.h:632-636).
            Assert.AreEqual(11, PacketCGItemUse.PacketHeader);
            Assert.AreEqual(4, PacketCGItemUse.PacketSize);
        }

        [Test]
        public void Serialize_GoldenBytes()
        {
            byte[] bytes = PacketCGItemUseCodec.Serialize(
                new PacketCGItemUse(ItemWindow.Inventory, 5));
            CollectionAssert.AreEqual(new byte[] { 0x0B, 0x01, 0x05, 0x00 }, bytes);
        }

        [Test]
        public void RoundTrip_PreservesCell()
        {
            var original = new PacketCGItemUse(ItemWindow.Equipment, 0x1234);
            PacketCGItemUse restored = PacketCGItemUseCodec.Deserialize(
                PacketCGItemUseCodec.Serialize(original));

            Assert.AreEqual(original.Window, restored.Window);
            Assert.AreEqual(original.Cell, restored.Cell);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGItemUseCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGItemUseCodec.Deserialize(new byte[] { 0x0C, 0x01, 0x00, 0x00 });
            });
        }
    }
}
