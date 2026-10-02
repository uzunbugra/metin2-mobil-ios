using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGItemDrop2Tests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:30 / client Packet.h:30: header 20;
            // struct = 1 + 3 + 4 + 1 = 9 (packet.h:652-658).
            Assert.AreEqual(20, PacketCGItemDrop2.PacketHeader);
            Assert.AreEqual(9, PacketCGItemDrop2.PacketSize);
        }

        [Test]
        public void Serialize_PartialDrop_GoldenBytes()
        {
            byte[] bytes = PacketCGItemDrop2Codec.Serialize(
                new PacketCGItemDrop2(ItemWindow.Inventory, 5, 0, 3));
            CollectionAssert.AreEqual(
                new byte[] { 0x14, 0x01, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03 },
                bytes);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketCGItemDrop2(ItemWindow.Inventory, 12, 0, 7);
            PacketCGItemDrop2 restored = PacketCGItemDrop2Codec.Deserialize(
                PacketCGItemDrop2Codec.Serialize(original));

            Assert.AreEqual(original.Window, restored.Window);
            Assert.AreEqual(original.Cell, restored.Cell);
            Assert.AreEqual(original.Gold, restored.Gold);
            Assert.AreEqual(original.Count, restored.Count);
        }

        [TestCase(0)]
        [TestCase(6)]
        [TestCase(8)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGItemDrop2Codec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] wrong = PacketCGItemDrop2Codec.Serialize(
                new PacketCGItemDrop2(ItemWindow.Inventory, 1, 0, 1));
            wrong[0] = 0x0C;
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGItemDrop2Codec.Deserialize(wrong);
            });
        }
    }
}
