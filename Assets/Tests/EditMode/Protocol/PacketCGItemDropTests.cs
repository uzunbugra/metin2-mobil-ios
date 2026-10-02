using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGItemDropTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:22 / client Packet.h:23: header 12;
            // struct = 1 + 3 + 4 = 8 (packet.h:645-650).
            Assert.AreEqual(12, PacketCGItemDrop.PacketHeader);
            Assert.AreEqual(8, PacketCGItemDrop.PacketSize);
        }

        [Test]
        public void Serialize_ItemDrop_GoldenBytes()
        {
            byte[] bytes = PacketCGItemDropCodec.Serialize(
                new PacketCGItemDrop(ItemWindow.Inventory, 5));
            CollectionAssert.AreEqual(
                new byte[] { 0x0C, 0x01, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00 },
                bytes);
        }

        [Test]
        public void Serialize_GoldDrop_GoldenBytes()
        {
            byte[] bytes = PacketCGItemDropCodec.Serialize(
                new PacketCGItemDrop(ItemWindow.Reserved, ushort.MaxValue, 42));
            CollectionAssert.AreEqual(
                new byte[] { 0x0C, 0x00, 0xFF, 0xFF, 0x2A, 0x00, 0x00, 0x00 },
                bytes);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketCGItemDrop(ItemWindow.Inventory, 9, 123456);
            PacketCGItemDrop restored = PacketCGItemDropCodec.Deserialize(
                PacketCGItemDropCodec.Serialize(original));

            Assert.AreEqual(original.Window, restored.Window);
            Assert.AreEqual(original.Cell, restored.Cell);
            Assert.AreEqual(original.Gold, restored.Gold);
        }

        [TestCase(0)]
        [TestCase(5)]
        [TestCase(7)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGItemDropCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] wrong = PacketCGItemDropCodec.Serialize(new PacketCGItemDrop(ItemWindow.Inventory, 1));
            wrong[0] = 0x0D;
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGItemDropCodec.Deserialize(wrong);
            });
        }
    }
}
