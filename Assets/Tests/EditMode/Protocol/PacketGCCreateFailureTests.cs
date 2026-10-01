using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCCreateFailureTests
    {
        [Test]
        public void PacketGCCreateFailure_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:862-866: 1+1 = 2.
            Assert.AreEqual(9, PacketGCCreateFailure.PacketHeader);
            Assert.AreEqual(2, PacketGCCreateFailure.PacketSize);
        }

        [TestCase((byte)0)]
        [TestCase((byte)1)]
        public void RoundTrip_PreservesType(byte type)
        {
            byte[] bytes = PacketGCCreateFailureCodec.Serialize(new PacketGCCreateFailure(type));

            CollectionAssert.AreEqual(new byte[] { 0x09, type }, bytes);

            PacketGCCreateFailure restored = PacketGCCreateFailureCodec.Deserialize(bytes);
            Assert.AreEqual(type, restored.Type);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x09;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCCreateFailureCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCCreateFailureCodec.Deserialize(new byte[] { 0x08, 0x00 });
            });
        }
    }
}
