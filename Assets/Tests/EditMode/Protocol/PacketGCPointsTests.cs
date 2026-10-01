using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCPointsTests
    {
        [Test]
        public void PacketGCPoints_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:1030-1034, POINT_MAX_NUM=255: 1+4*255 = 1021.
            Assert.AreEqual(16, PacketGCPoints.PacketHeader);
            Assert.AreEqual(255, PacketGCPoints.PointCount);
            Assert.AreEqual(1021, PacketGCPoints.PacketSize);
        }

        [Test]
        public void Serialize_SpotsCheckKeyOffsets()
        {
            var points = new int[255];
            points[0] = 35; // level
            points[3] = 123456; // hp?
            points[254] = -1;
            byte[] bytes = PacketGCPointsCodec.Serialize(new PacketGCPoints(points));

            Assert.AreEqual(1021, bytes.Length);
            Assert.AreEqual(0x10, bytes[0]);
            // points[0] LE at [1..4].
            Assert.AreEqual(35, bytes[1]);
            Assert.AreEqual(0x00, bytes[4]);
            // points[3] = 123456 = 0x1E240 at [13..16] LE.
            Assert.AreEqual(0x40, bytes[13]);
            Assert.AreEqual(0xE2, bytes[14]);
            Assert.AreEqual(0x01, bytes[15]);
            Assert.AreEqual(0x00, bytes[16]);
            // points[254] = -1 at [1017..1020].
            Assert.AreEqual(0xFF, bytes[1017]);
            Assert.AreEqual(0xFF, bytes[1020]);
        }

        [Test]
        public void RoundTrip_PreservesAllPoints()
        {
            var original = new int[255];
            for (int i = 0; i < original.Length; i++)
            {
                original[i] = i * -37 + 11;
            }

            PacketGCPoints restored =
                PacketGCPointsCodec.Deserialize(PacketGCPointsCodec.Serialize(new PacketGCPoints(original)));

            CollectionAssert.AreEqual(original, restored.Points);
        }

        [TestCase(0)]
        [TestCase(1020)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x10;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCPointsCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[1021];
            badHeader[0] = 0x4c;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCPointsCodec.Deserialize(badHeader);
            });
        }
    }
}
