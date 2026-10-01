using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGEnterGameTests
    {
        [Test]
        public void PacketCGEnterGame_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:627-630 (header packet.h:19).
            Assert.AreEqual(10, PacketCGEnterGame.PacketHeader);
            Assert.AreEqual(1, PacketCGEnterGame.PacketSize);
        }

        [Test]
        public void RoundTrip_SingleHeaderByte()
        {
            byte[] bytes = PacketCGEnterGameCodec.Serialize(new PacketCGEnterGame());

            CollectionAssert.AreEqual(new byte[] { 0x0a }, bytes);
            Assert.IsTrue(PacketCGEnterGameCodec.TryDeserialize(bytes, out PacketCGEnterGame _, out string _));
        }

        [Test]
        public void Deserialize_EmptyBuffer_ThrowsPacketUnderflowException()
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGEnterGameCodec.Deserialize(new byte[0]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGEnterGameCodec.Deserialize(new byte[] { 0x06 });
            });
        }
    }
}
