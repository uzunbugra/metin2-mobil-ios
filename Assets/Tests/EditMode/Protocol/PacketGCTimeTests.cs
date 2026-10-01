using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCTimeTests
    {
        [Test]
        public void PacketGCTime_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:1891-1895 (header packet.h:218):
            // time_t is 4B both sides (32-bit server, _USE_32BIT_TIME_T client).
            Assert.AreEqual(106, PacketGCTime.PacketHeader);
            Assert.AreEqual(5, PacketGCTime.PacketSize);
        }

        [Test]
        public void Serialize_ProducesExactGoldenBytes()
        {
            // 0x64E3B200 LE.
            byte[] bytes = PacketGCTimeCodec.Serialize(new PacketGCTime(0x64E3B200));

            CollectionAssert.AreEqual(new byte[] { 0x6a, 0x00, 0xB2, 0xE3, 0x64 }, bytes);
        }

        [Test]
        public void RoundTrip_PreservesTime()
        {
            var original = new PacketGCTime(1727712000);
            PacketGCTime restored =
                PacketGCTimeCodec.Deserialize(PacketGCTimeCodec.Serialize(original));

            Assert.AreEqual(1727712000u, restored.Time);
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x6a;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCTimeCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCTimeCodec.Deserialize(new byte[] { 0x79, 0x00, 0x00, 0x00, 0x00 });
            });
        }
    }
}
