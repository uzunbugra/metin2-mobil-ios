using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGItemUseToItemTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:46 / client Packet.h:70: header 60;
            // struct = 1 + 3 + 3 = 7 (packet.h:638-643).
            Assert.AreEqual(60, PacketCGItemUseToItem.PacketHeader);
            Assert.AreEqual(7, PacketCGItemUseToItem.PacketSize);
        }

        [Test]
        public void Serialize_GoldenBytes()
        {
            byte[] bytes = PacketCGItemUseToItemCodec.Serialize(
                new PacketCGItemUseToItem(ItemWindow.Inventory, 5, ItemWindow.Equipment, 6));
            CollectionAssert.AreEqual(
                new byte[] { 0x3C, 0x01, 0x05, 0x00, 0x02, 0x06, 0x00 },
                bytes);
        }

        [Test]
        public void RoundTrip_PreservesBothCells()
        {
            var original = new PacketCGItemUseToItem(
                ItemWindow.Inventory, 0x1111, ItemWindow.Inventory, 0x2222);
            PacketCGItemUseToItem restored = PacketCGItemUseToItemCodec.Deserialize(
                PacketCGItemUseToItemCodec.Serialize(original));

            Assert.AreEqual(original.Window, restored.Window);
            Assert.AreEqual(original.Cell, restored.Cell);
            Assert.AreEqual(original.TargetWindow, restored.TargetWindow);
            Assert.AreEqual(original.TargetCell, restored.TargetCell);
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(6)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGItemUseToItemCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] wrong = PacketCGItemUseToItemCodec.Serialize(
                new PacketCGItemUseToItem(ItemWindow.Inventory, 1, ItemWindow.Inventory, 2));
            wrong[0] = 0x42;
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGItemUseToItemCodec.Deserialize(wrong);
            });
        }
    }
}
