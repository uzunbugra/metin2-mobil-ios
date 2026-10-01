using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCItemUpdateTests
    {
        [Test]
        public void PacketGCItemUpdate_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:1108-1115: 1+3+1+12+21 = 38.
            Assert.AreEqual(25, PacketGCItemUpdate.PacketHeader);
            Assert.AreEqual(38, PacketGCItemUpdate.PacketSize);
        }

        [Test]
        public void Serialize_ProducesExactWireLayout()
        {
            var packet = new PacketGCItemUpdate(
                1, 5, 200,
                new int[] { 1, 2, 3 },
                new ItemAttribute[] { new ItemAttribute { Type = 5, Value = -20 } });
            byte[] bytes = PacketGCItemUpdateCodec.Serialize(packet);

            Assert.AreEqual(38, bytes.Length);
            Assert.AreEqual(0x19, bytes[0]);
            Assert.AreEqual(0x01, bytes[1]);
            Assert.AreEqual(0x05, bytes[2]);
            // count 200 at [4].
            Assert.AreEqual(0xC8, bytes[4]);
            // socket0 = 1 at [5..8] LE.
            Assert.AreEqual(0x01, bytes[5]);
            // attr0: type 5 at [17], value -20 = 0xFFEC at [18..19] LE.
            Assert.AreEqual(0x05, bytes[17]);
            Assert.AreEqual(0xEC, bytes[18]);
            Assert.AreEqual(0xFF, bytes[19]);
        }

        [Test]
        public void RoundTrip_PreservesMutation()
        {
            var original = new PacketGCItemUpdate(
                1, 10, 99, new int[] { 7, 8, 9 },
                new ItemAttribute[] { new ItemAttribute { Type = 2, Value = 300 } });
            PacketGCItemUpdate restored =
                PacketGCItemUpdateCodec.Deserialize(PacketGCItemUpdateCodec.Serialize(original));

            Assert.AreEqual(1, restored.Window);
            Assert.AreEqual(10, restored.Cell);
            Assert.AreEqual(99, restored.Count);
            CollectionAssert.AreEqual(new int[] { 7, 8, 9 }, restored.Sockets);
            Assert.IsTrue(new ItemAttribute { Type = 2, Value = 300 }.Equals(restored.Attributes[0]));
        }

        [TestCase(0)]
        [TestCase(37)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x19;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCItemUpdateCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[38];
            badHeader[0] = 0x15;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCItemUpdateCodec.Deserialize(badHeader);
            });
        }
    }
}
