using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCPingTests
    {
        [Test]
        public void PacketGCPing_Constants_MatchServerSource()
        {
            // Server packet.h:1259-1262: TPacketGCPing = BYTE header only;
            // header value 44 (packet.h:172, client Packet.h:195).
            Assert.AreEqual(44, PacketGCPing.PacketHeader);
            Assert.AreEqual(1, PacketGCPing.PacketSize);
        }

        [Test]
        public void Serialize_GoldenByte()
        {
            byte[] bytes = PacketGCPingCodec.Serialize(new PacketGCPing());
            CollectionAssert.AreEqual(new byte[] { 44 }, bytes);
        }

        [Test]
        public void RoundTrip_HeaderOnly()
        {
            byte[] bytes = PacketGCPingCodec.Serialize(new PacketGCPing());
            PacketGCPing restored = PacketGCPingCodec.Deserialize(bytes);
            Assert.AreEqual(44, restored.Header);
            Assert.AreEqual(1, restored.Length);
        }

        [Test]
        public void TryDeserialize_EmptyBuffer_Fails()
        {
            Assert.IsFalse(PacketGCPingCodec.TryDeserialize(new byte[0], out _, out string error));
            StringAssert.Contains("truncated", error);
        }

        [Test]
        public void Deserialize_EmptyBuffer_ThrowsPacketUnderflowException()
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCPingCodec.Deserialize(new byte[0]);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            // 0xfe is NOT GC_PING (it is CG_PONG on the C2S side and
            // GC_BINDUDP on the S2C side, packet.h:115) — regression guard
            // for the old wrong constant.
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCPingCodec.Deserialize(new byte[] { 0xfe });
            });
        }
    }
}
