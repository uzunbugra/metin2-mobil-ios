using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGMoveTests
    {
        [Test]
        public void PacketCGMove_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:586-595 (header packet.h:17):
            // 1+3+8+4 = 16.
            Assert.AreEqual(7, PacketCGMove.PacketHeader);
            Assert.AreEqual(16, PacketCGMove.PacketSize);
            Assert.AreEqual(1, MoveFunc.Move);
            Assert.AreEqual(0x80, MoveFunc.Skill);
        }

        [Test]
        public void Serialize_ProducesExactWireLayout()
        {
            var packet = new PacketCGMove(MoveFunc.Move, 0, 18, 474387, 954234, 0x01020304);
            byte[] bytes = PacketCGMoveCodec.Serialize(packet);

            Assert.AreEqual(16, bytes.Length);
            Assert.AreEqual(0x07, bytes[0]);
            Assert.AreEqual(0x01, bytes[1]);
            Assert.AreEqual(0x00, bytes[2]);
            Assert.AreEqual(0x12, bytes[3]);
            // x = 474387 = 0x73D13 at [4..7] LE.
            Assert.AreEqual(0x13, bytes[4]);
            Assert.AreEqual(0x3D, bytes[5]);
            Assert.AreEqual(0x07, bytes[6]);
            Assert.AreEqual(0x00, bytes[7]);
            // time at [12..15].
            Assert.AreEqual(0x04, bytes[12]);
            Assert.AreEqual(0x01, bytes[15]);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketCGMove(MoveFunc.Attack, 3, 36, 100, 200, 9999);
            PacketCGMove restored =
                PacketCGMoveCodec.Deserialize(PacketCGMoveCodec.Serialize(original));

            Assert.AreEqual(original.Func, restored.Func);
            Assert.AreEqual(original.Arg, restored.Arg);
            Assert.AreEqual(original.Rot, restored.Rot);
            Assert.AreEqual(original.X, restored.X);
            Assert.AreEqual(original.Y, restored.Y);
            Assert.AreEqual(original.Time, restored.Time);
        }

        [TestCase(0)]
        [TestCase(15)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x07;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGMoveCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[16];
            badHeader[0] = 0x03;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGMoveCodec.Deserialize(badHeader);
            });
        }
    }
}
