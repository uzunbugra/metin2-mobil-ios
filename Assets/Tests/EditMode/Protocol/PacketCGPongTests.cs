using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGPongTests
    {
        [Test]
        public void PacketCGPong_Constants_MatchClientSource()
        {
            // Client Packet.h:1844-1847: TPacketCGPong = BYTE header only;
            // header value 0xfe (packet.h:7, client Packet.h:138).
            Assert.AreEqual(0xfe, PacketCGPong.PacketHeader);
            Assert.AreEqual(1, PacketCGPong.PacketSize);
        }

        [Test]
        public void Serialize_GoldenByte()
        {
            byte[] bytes = PacketCGPongCodec.Serialize(new PacketCGPong());
            CollectionAssert.AreEqual(new byte[] { 0xfe }, bytes);
        }

        [Test]
        public void RoundTrip_HeaderOnly()
        {
            byte[] bytes = PacketCGPongCodec.Serialize(new PacketCGPong());
            PacketCGPong restored = PacketCGPongCodec.Deserialize(bytes);
            Assert.AreEqual(0xfe, restored.Header);
            Assert.AreEqual(1, restored.Length);
        }

        [Test]
        public void TryDeserialize_EmptyBuffer_Fails()
        {
            Assert.IsFalse(PacketCGPongCodec.TryDeserialize(new byte[0], out _, out string error));
            StringAssert.Contains("truncated", error);
        }

        [Test]
        public void Deserialize_EmptyBuffer_ThrowsPacketUnderflowException()
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGPongCodec.Deserialize(new byte[0]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGPongCodec.Deserialize(new byte[] { 44 });
            });
        }
    }
}
