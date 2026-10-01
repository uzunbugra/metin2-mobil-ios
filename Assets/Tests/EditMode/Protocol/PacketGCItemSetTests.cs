using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCItemSetTests
    {
        [Test]
        public void PacketGCItemSet_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:1073-1084: 1+3+4+1+4+4+1+12+21 = 51.
            Assert.AreEqual(21, PacketGCItemSet.PacketHeader);
            Assert.AreEqual(51, PacketGCItemSet.PacketSize);
        }

        private static PacketGCItemSet SamplePacket()
        {
            return new PacketGCItemSet(
                1, 5, 11243, 1, 0x00000004u, 0x00000100u, false,
                new int[] { 28448, 0, 0 },
                new ItemAttribute[]
                {
                    new ItemAttribute { Type = 1, Value = 15 },
                    new ItemAttribute { Type = 7, Value = 10 }
                });
        }

        [Test]
        public void Serialize_ProducesExactWireLayout()
        {
            byte[] bytes = PacketGCItemSetCodec.Serialize(SamplePacket());

            Assert.AreEqual(51, bytes.Length);
            Assert.AreEqual(0x15, bytes[0]);
            // Cell: window 1 at [1], cell 5 LE at [2..3].
            Assert.AreEqual(0x01, bytes[1]);
            Assert.AreEqual(0x05, bytes[2]);
            Assert.AreEqual(0x00, bytes[3]);
            // vnum 11243 = 0x2BEB at [4..7] LE.
            Assert.AreEqual(0xEB, bytes[4]);
            Assert.AreEqual(0x2B, bytes[5]);
            // count at [8], flags at [9..12] = 4.
            Assert.AreEqual(0x01, bytes[8]);
            Assert.AreEqual(0x04, bytes[9]);
            // anti_flags 0x100 at [13..16].
            Assert.AreEqual(0x00, bytes[13]);
            Assert.AreEqual(0x01, bytes[14]);
            // highlight 0 at [17].
            Assert.AreEqual(0x00, bytes[17]);
            // socket0 28448 = 0x6F20 at [18..21] LE.
            Assert.AreEqual(0x20, bytes[18]);
            Assert.AreEqual(0x6F, bytes[19]);
            // attr0 type/value at [30..32]: 1, 15 LE.
            Assert.AreEqual(0x01, bytes[30]);
            Assert.AreEqual(0x0F, bytes[31]);
            Assert.AreEqual(0x00, bytes[32]);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            PacketGCItemSet original = SamplePacket();
            PacketGCItemSet restored =
                PacketGCItemSetCodec.Deserialize(PacketGCItemSetCodec.Serialize(original));

            Assert.AreEqual(original.Window, restored.Window);
            Assert.AreEqual(original.Cell, restored.Cell);
            Assert.AreEqual(original.Vnum, restored.Vnum);
            Assert.AreEqual(original.Count, restored.Count);
            Assert.AreEqual(original.Flags, restored.Flags);
            Assert.AreEqual(original.AntiFlags, restored.AntiFlags);
            Assert.AreEqual(original.Highlight, restored.Highlight);
            CollectionAssert.AreEqual(original.Sockets, restored.Sockets);
            for (int i = 0; i < 7; i++)
            {
                Assert.IsTrue(original.Attributes[i].Equals(restored.Attributes[i]), $"Attr {i} mismatch.");
            }
        }

        [TestCase(0)]
        [TestCase(50)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x15;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCItemSetCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[51];
            badHeader[0] = 0x14;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCItemSetCodec.Deserialize(badHeader);
            });
        }
    }
}
