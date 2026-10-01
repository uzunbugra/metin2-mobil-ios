using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCMoveTests
    {
        [Test]
        public void PacketGCMove_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:1288-1299:
            // 1+3+4+4+4+4+4 = 24.
            Assert.AreEqual(3, PacketGCMove.PacketHeader);
            Assert.AreEqual(24, PacketGCMove.PacketSize);
        }

        [Test]
        public void Serialize_ProducesExactWireLayout()
        {
            var packet = new PacketGCMove
            {
                Func = MoveFunc.Move,
                Arg = 0,
                Rot = 18,
                Vid = 0x0000BEEF,
                X = 474387,
                Y = 954234,
                Time = 0x01020304,
                Duration = 500
            };
            byte[] bytes = PacketGCMoveCodec.Serialize(packet);

            Assert.AreEqual(24, bytes.Length);
            Assert.AreEqual(0x03, bytes[0]);
            Assert.AreEqual(0x01, bytes[1]);
            // vid LE at [4..7] = EF BE 00 00.
            Assert.AreEqual(0xEF, bytes[4]);
            Assert.AreEqual(0xBE, bytes[5]);
            // duration 500 = 0x1F4 at [20..23] LE.
            Assert.AreEqual(0xF4, bytes[20]);
            Assert.AreEqual(0x01, bytes[21]);
            Assert.AreEqual(0x00, bytes[23]);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketGCMove
            {
                Func = MoveFunc.Combo,
                Arg = 2,
                Rot = 40,
                Vid = 123,
                X = 1,
                Y = 2,
                Time = 3,
                Duration = 0
            };
            PacketGCMove restored =
                PacketGCMoveCodec.Deserialize(PacketGCMoveCodec.Serialize(original));

            Assert.AreEqual(original.Func, restored.Func);
            Assert.AreEqual(original.Arg, restored.Arg);
            Assert.AreEqual(original.Rot, restored.Rot);
            Assert.AreEqual(original.Vid, restored.Vid);
            Assert.AreEqual(original.X, restored.X);
            Assert.AreEqual(original.Y, restored.Y);
            Assert.AreEqual(original.Time, restored.Time);
            Assert.AreEqual(original.Duration, restored.Duration);
        }

        [TestCase(0)]
        [TestCase(23)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x03;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCMoveCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[24];
            badHeader[0] = 0x07;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCMoveCodec.Deserialize(badHeader);
            });
        }
    }
}
