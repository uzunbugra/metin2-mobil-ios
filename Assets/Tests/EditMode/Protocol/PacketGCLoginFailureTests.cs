using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCLoginFailureTests
    {
        [Test]
        public void PacketGCLoginFailure_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:856-860 + common/length.h:12 (8+1).
            Assert.AreEqual(7, PacketGCLoginFailure.PacketHeader);
            Assert.AreEqual(9, PacketGCLoginFailure.StatusBufferLen);
            Assert.AreEqual(10, PacketGCLoginFailure.PacketSize);
        }

        [TestCase("NOID", new byte[] { 0x07, (byte)'N', (byte)'O', (byte)'I', (byte)'D', 0x00, 0x00, 0x00, 0x00, 0x00 })]
        [TestCase("ALREADY", new byte[] { 0x07, (byte)'A', (byte)'L', (byte)'R', (byte)'E', (byte)'A', (byte)'D', (byte)'Y', 0x00, 0x00 })]
        [TestCase("WRONGPWD", new byte[] { 0x07, (byte)'W', (byte)'R', (byte)'O', (byte)'N', (byte)'G', (byte)'P', (byte)'W', (byte)'D', 0x00 })]
        [TestCase("SHUTDOWN", new byte[] { 0x07, (byte)'S', (byte)'H', (byte)'U', (byte)'T', (byte)'D', (byte)'O', (byte)'W', (byte)'N', 0x00 })]
        public void Serialize_KnownStatuses_ProduceExactGoldenBytes(string status, byte[] expected)
        {
            // Statuses observed in input_auth.cpp / input_db.cpp / input.cpp.
            byte[] bytes = PacketGCLoginFailureCodec.Serialize(new PacketGCLoginFailure(status));

            CollectionAssert.AreEqual(expected, bytes);
        }

        [TestCase("NOID")]
        [TestCase("ALREADY")]
        [TestCase("WRONGPWD")]
        [TestCase("SHUTDOWN")]
        public void RoundTrip_PreservesStatus(string status)
        {
            // NOTE: "BESAMEKEY" (9 chars) is intentionally excluded: the wire
            // buffer holds 8 chars + null (length.h:12), so the server can only
            // send up to 8 chars. "BESAMEKEY" is a client-side synthetic status
            // for the 150/bResult==0 path (AccountConnector.cpp:321) and never
            // travels as a 10-byte failure packet.
            var original = new PacketGCLoginFailure(status);
            PacketGCLoginFailure restored =
                PacketGCLoginFailureCodec.Deserialize(PacketGCLoginFailureCodec.Serialize(original));

            Assert.AreEqual(status, restored.Status);
        }

        [Test]
        public void Serialize_OverlongStatus_IsTruncatedSafely()
        {
            // strlcpy semantics on the server side (input.cpp:185): never overflows.
            byte[] bytes = PacketGCLoginFailureCodec.Serialize(new PacketGCLoginFailure("TOOLONGSTATUS"));

            Assert.AreEqual(10, bytes.Length);
            Assert.AreEqual(0x07, bytes[0]);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(9)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x07;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCLoginFailureCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[] { 0x96, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCLoginFailureCodec.Deserialize(badHeader);
            });
        }
    }
}
