using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCDeleteSuccessTests
    {
        [Test]
        public void PacketGCDeleteSuccess_Constants_MatchServerSource()
        {
            // input_db.cpp:285-286: two 1-byte Packet() calls (header 10 + index).
            Assert.AreEqual(10, PacketGCDeleteSuccess.PacketHeader);
            Assert.AreEqual(2, PacketGCDeleteSuccess.PacketSize);
        }

        [TestCase((byte)0)]
        [TestCase((byte)3)]
        public void RoundTrip_PreservesIndex(byte index)
        {
            byte[] bytes = PacketGCDeleteSuccessCodec.Serialize(new PacketGCDeleteSuccess(index));

            CollectionAssert.AreEqual(new byte[] { 0x0a, index }, bytes);

            PacketGCDeleteSuccess restored = PacketGCDeleteSuccessCodec.Deserialize(bytes);
            Assert.AreEqual(index, restored.Index);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x0a;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCDeleteSuccessCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCDeleteSuccessCodec.Deserialize(new byte[] { 0x0b, 0x00 });
            });
        }
    }
}
