using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCChannelTests
    {
        [Test]
        public void PacketGCChannel_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:1995-1999 (header packet.h:234).
            Assert.AreEqual(121, PacketGCChannel.PacketHeader);
            Assert.AreEqual(2, PacketGCChannel.PacketSize);
        }

        [TestCase((byte)1)]
        [TestCase((byte)4)]
        public void RoundTrip_PreservesChannel(byte channel)
        {
            byte[] bytes = PacketGCChannelCodec.Serialize(new PacketGCChannel(channel));

            CollectionAssert.AreEqual(new byte[] { 0x79, channel }, bytes);

            PacketGCChannel restored = PacketGCChannelCodec.Deserialize(bytes);
            Assert.AreEqual(channel, restored.Channel);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x79;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCChannelCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCChannelCodec.Deserialize(new byte[] { 0x6a, 0x01 });
            });
        }
    }
}
