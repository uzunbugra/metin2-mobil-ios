using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGItemMoveTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:23 / client Packet.h:24: header 13;
            // struct = 1 + 3 + 3 + 1 = 8 (packet.h:660-666).
            Assert.AreEqual(13, PacketCGItemMove.PacketHeader);
            Assert.AreEqual(8, PacketCGItemMove.PacketSize);
        }

        [Test]
        public void Serialize_StackMerge_GoldenBytes()
        {
            byte[] bytes = PacketCGItemMoveCodec.Serialize(
                new PacketCGItemMove(ItemWindow.Inventory, 5, ItemWindow.Inventory, 10, 3));
            CollectionAssert.AreEqual(
                new byte[] { 0x0D, 0x01, 0x05, 0x00, 0x01, 0x0A, 0x00, 0x03 },
                bytes);
        }

        [Test]
        public void Serialize_WholeStackMove_CountZero()
        {
            byte[] bytes = PacketCGItemMoveCodec.Serialize(
                new PacketCGItemMove(ItemWindow.Inventory, 1, ItemWindow.Equipment, 45));
            CollectionAssert.AreEqual(
                new byte[] { 0x0D, 0x01, 0x01, 0x00, 0x02, 0x2D, 0x00, 0x00 },
                bytes);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketCGItemMove(
                ItemWindow.BeltInventory, 0x0102, ItemWindow.DragonSoulInventory, 0x0304, 200);
            PacketCGItemMove restored = PacketCGItemMoveCodec.Deserialize(
                PacketCGItemMoveCodec.Serialize(original));

            Assert.AreEqual(original.Window, restored.Window);
            Assert.AreEqual(original.Cell, restored.Cell);
            Assert.AreEqual(original.WindowTo, restored.WindowTo);
            Assert.AreEqual(original.CellTo, restored.CellTo);
            Assert.AreEqual(original.Count, restored.Count);
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(7)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGItemMoveCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] wrong = PacketCGItemMoveCodec.Serialize(
                new PacketCGItemMove(ItemWindow.Inventory, 1, ItemWindow.Inventory, 2));
            wrong[0] = 0x42;
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGItemMoveCodec.Deserialize(wrong);
            });
        }
    }
}
