using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCEmpireTests
    {
        [Test]
        public void PacketGCEmpire_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:1638-1642 (header packet.h:206).
            Assert.AreEqual(90, PacketGCEmpire.PacketHeader);
            Assert.AreEqual(2, PacketGCEmpire.PacketSize);
        }

        [TestCase((byte)1)]
        [TestCase((byte)2)]
        [TestCase((byte)3)]
        public void RoundTrip_PreservesEmpire(byte empire)
        {
            var original = new PacketGCEmpire(empire);
            byte[] bytes = PacketGCEmpireCodec.Serialize(original);

            Assert.AreEqual(2, bytes.Length);
            Assert.AreEqual(0x5a, bytes[0]);
            Assert.AreEqual(empire, bytes[1]);

            PacketGCEmpire restored = PacketGCEmpireCodec.Deserialize(bytes);
            Assert.AreEqual(empire, restored.Empire);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x5a;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCEmpireCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCEmpireCodec.Deserialize(new byte[] { 0x20, 0x01 });
            });
        }
    }
}
