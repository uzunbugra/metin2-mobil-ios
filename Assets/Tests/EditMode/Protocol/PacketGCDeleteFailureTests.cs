using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCDeleteFailureTests
    {
        [Test]
        public void PacketGCDeleteFailure_Constants_MatchServerSource()
        {
            // Single 1-byte Packet() of header 11 (input_db.cpp:296).
            Assert.AreEqual(11, PacketGCDeleteFailure.PacketHeader);
            Assert.AreEqual(1, PacketGCDeleteFailure.PacketSize);
        }

        [Test]
        public void RoundTrip_SingleHeaderByte()
        {
            byte[] bytes = PacketGCDeleteFailureCodec.Serialize(new PacketGCDeleteFailure());

            CollectionAssert.AreEqual(new byte[] { 0x0b }, bytes);
            Assert.IsTrue(PacketGCDeleteFailureCodec.TryDeserialize(bytes, out PacketGCDeleteFailure _, out string _));
        }

        [Test]
        public void Deserialize_EmptyBuffer_ThrowsPacketUnderflowException()
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCDeleteFailureCodec.Deserialize(new byte[0]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCDeleteFailureCodec.Deserialize(new byte[] { 0x0a });
            });
        }
    }
}
