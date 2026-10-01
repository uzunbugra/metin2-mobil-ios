using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCCharacterDeleteTests
    {
        [Test]
        public void PacketGCCharacterDelete_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:959-963: 1+4 = 5.
            Assert.AreEqual(2, PacketGCCharacterDelete.PacketHeader);
            Assert.AreEqual(5, PacketGCCharacterDelete.PacketSize);
        }

        [Test]
        public void RoundTrip_PreservesVid()
        {
            byte[] bytes = PacketGCCharacterDeleteCodec.Serialize(new PacketGCCharacterDelete(0xDEADBEEF));

            CollectionAssert.AreEqual(new byte[] { 0x02, 0xEF, 0xBE, 0xAD, 0xDE }, bytes);

            PacketGCCharacterDelete restored = PacketGCCharacterDeleteCodec.Deserialize(bytes);
            Assert.AreEqual(0xDEADBEEFu, restored.Vid);
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x02;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCCharacterDeleteCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCCharacterDeleteCodec.Deserialize(new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00 });
            });
        }
    }
}
