using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGLogin2Tests
    {
        [Test]
        public void PacketCGLogin2_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:508-514 (header packet.h:84).
            Assert.AreEqual(109, PacketCGLogin2.PacketHeader);
            Assert.AreEqual(31, PacketCGLogin2.LoginBufferLen);
            Assert.AreEqual(52, PacketCGLogin2.PacketSize);
        }

        [Test]
        public void Serialize_ProducesExactGoldenBytes()
        {
            // Hand-computed: 0x6d + "ab"+zeros(29) + key LE + 4 zero keys.
            var packet = new PacketCGLogin2("ab", 0x12345678, new uint[4]);
            byte[] bytes = PacketCGLogin2Codec.Serialize(packet);

            Assert.AreEqual(52, bytes.Length);
            Assert.AreEqual(0x6d, bytes[0]);
            Assert.AreEqual((byte)'a', bytes[1]);
            Assert.AreEqual((byte)'b', bytes[2]);
            Assert.AreEqual(0x00, bytes[3]);
            Assert.AreEqual(0x78, bytes[32]);
            Assert.AreEqual(0x56, bytes[33]);
            Assert.AreEqual(0x34, bytes[34]);
            Assert.AreEqual(0x12, bytes[35]);
            for (int i = 36; i < 52; i++)
            {
                Assert.AreEqual(0x00, bytes[i]);
            }
        }

        [Test]
        public void RoundTrip_PreservesLoginKeyAndClientKeys()
        {
            var original = new PacketCGLogin2(
                "testuser", 0xDEADBEEF, new uint[] { 1, 2, 3, 4 });
            PacketCGLogin2 restored =
                PacketCGLogin2Codec.Deserialize(PacketCGLogin2Codec.Serialize(original));

            Assert.AreEqual("testuser", restored.Login);
            Assert.AreEqual(0xDEADBEEFu, restored.LoginKey);
            CollectionAssert.AreEqual(new uint[] { 1, 2, 3, 4 }, restored.ClientKeys);
        }

        [TestCase(0)]
        [TestCase(51)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x6d;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGLogin2Codec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[52];
            badHeader[0] = 0x6f; // LOGIN3, not LOGIN2

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGLogin2Codec.Deserialize(badHeader);
            });
        }
    }
}
