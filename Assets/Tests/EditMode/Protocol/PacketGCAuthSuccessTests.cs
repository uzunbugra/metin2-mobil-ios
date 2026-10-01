using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCAuthSuccessTests
    {
        [Test]
        public void PacketGCAuthSuccess_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:849-854, header game/src/packet.h:266.
            Assert.AreEqual(150, PacketGCAuthSuccess.PacketHeader);
            Assert.AreEqual(6, PacketGCAuthSuccess.PacketSize);
        }

        [Test]
        public void Serialize_ProducesExactGoldenBytes()
        {
            // Hand-computed from the struct layout (1B header + u32 LE key + 1B result).
            var packet = new PacketGCAuthSuccess(0x12345678, 1);
            byte[] bytes = PacketGCAuthSuccessCodec.Serialize(packet);

            CollectionAssert.AreEqual(
                new byte[] { 0x96, 0x78, 0x56, 0x34, 0x12, 0x01 }, bytes);
        }

        [Test]
        public void Deserialize_FromGoldenBytes_RestoresFields()
        {
            byte[] golden = new byte[] { 0x96, 0xEF, 0xBE, 0xAD, 0xDE, 0x01 };
            PacketGCAuthSuccess packet = PacketGCAuthSuccessCodec.Deserialize(golden);

            Assert.AreEqual(0x96, packet.Header);
            Assert.AreEqual(0xDEADBEEFu, packet.LoginKey);
            Assert.AreEqual(1, packet.Result);
            Assert.IsTrue(packet.Succeeded);
        }

        [Test]
        public void Deserialize_ZeroResult_MeansFailure()
        {
            // input_db.cpp:1703-1706 sends dwLoginKey=0, bResult=0 on DB failure.
            byte[] golden = new byte[] { 0x96, 0x00, 0x00, 0x00, 0x00, 0x00 };
            PacketGCAuthSuccess packet = PacketGCAuthSuccessCodec.Deserialize(golden);

            Assert.IsFalse(packet.Succeeded);
            Assert.AreEqual(0u, packet.LoginKey);
        }

        [Test]
        public void RoundTrip_PreservesFields()
        {
            var original = new PacketGCAuthSuccess(987654321, 1);
            PacketGCAuthSuccess restored =
                PacketGCAuthSuccessCodec.Deserialize(PacketGCAuthSuccessCodec.Serialize(original));

            Assert.AreEqual(original.LoginKey, restored.LoginKey);
            Assert.AreEqual(original.Result, restored.Result);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x96;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCAuthSuccessCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[] { 0x07, 0x00, 0x00, 0x00, 0x00, 0x01 };

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCAuthSuccessCodec.Deserialize(badHeader);
            });
        }

        [Test]
        public void TryDeserialize_Truncated_ReturnsFalseWithMessage()
        {
            bool ok = PacketGCAuthSuccessCodec.TryDeserialize(
                new byte[] { 0x96, 0x01 }, out PacketGCAuthSuccess _, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }
    }
}
