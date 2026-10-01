using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCItemDelTests
    {
        [Test]
        public void PacketGCItemDel_Constants_MatchServerSource()
        {
            // Server TPacketGCItemDelDeprecated, packet.h:1063-1071:
            // 1+3+4+1+12+21 = 42 (NOT the 2-byte packet_item_del).
            Assert.AreEqual(20, PacketGCItemDel.PacketHeader);
            Assert.AreEqual(42, PacketGCItemDel.PacketSize);
        }

        [Test]
        public void Serialize_ClearFrame_IsZeroedAfterCell()
        {
            // char_item.cpp:426-437 sends vnum/count/sockets/attrs zeroed.
            byte[] bytes = PacketGCItemDelCodec.Serialize(
                new PacketGCItemDel(1, 5, 0, 0, new int[3], new ItemAttribute[7]));

            Assert.AreEqual(42, bytes.Length);
            Assert.AreEqual(0x14, bytes[0]);
            Assert.AreEqual(0x01, bytes[1]);
            Assert.AreEqual(0x05, bytes[2]);
            for (int i = 4; i < 42; i++)
            {
                Assert.AreEqual(0x00, bytes[i], $"Byte {i} must be zero on clear.");
            }
        }

        [Test]
        public void RoundTrip_PreservesCell()
        {
            var original = new PacketGCItemDel(2, 190, 0, 0, null, null);
            PacketGCItemDel restored =
                PacketGCItemDelCodec.Deserialize(PacketGCItemDelCodec.Serialize(original));

            Assert.AreEqual(2, restored.Window);
            Assert.AreEqual(190, restored.Cell);
            Assert.AreEqual(0u, restored.Vnum);
        }

        [TestCase(0)]
        [TestCase(41)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x14;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCItemDelCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[42];
            badHeader[0] = 0x15;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCItemDelCodec.Deserialize(badHeader);
            });
        }
    }
}
