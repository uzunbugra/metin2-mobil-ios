using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCCreateSuccessTests
    {
        [Test]
        public void PacketGCCreateSuccess_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:556-561: 1+1+63 = 65.
            Assert.AreEqual(8, PacketGCCreateSuccess.PacketHeader);
            Assert.AreEqual(65, PacketGCCreateSuccess.PacketSize);
        }

        [Test]
        public void Serialize_ProducesExactWireLayout()
        {
            var packet = new PacketGCCreateSuccess(2, SimplePlayerCodecTests.SampleSlot());
            byte[] bytes = PacketGCCreateSuccessCodec.Serialize(packet);

            Assert.AreEqual(65, bytes.Length);
            Assert.AreEqual(0x08, bytes[0]);
            Assert.AreEqual(0x02, bytes[1]);
            // Embedded slot id LE at [2..5] = 12345 = 0x3039.
            Assert.AreEqual(0x39, bytes[2]);
            Assert.AreEqual(0x30, bytes[3]);
            Assert.AreEqual((byte)'W', bytes[6]);
        }

        [Test]
        public void RoundTrip_PreservesSlotAndPlayer()
        {
            var original = new PacketGCCreateSuccess(1, SimplePlayerCodecTests.SampleSlot());
            PacketGCCreateSuccess restored =
                PacketGCCreateSuccessCodec.Deserialize(PacketGCCreateSuccessCodec.Serialize(original));

            Assert.AreEqual(1, restored.Slot);
            Assert.IsTrue(original.Player.Equals(restored.Player));
        }

        [TestCase(0)]
        [TestCase(64)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x08;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCCreateSuccessCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[65];
            badHeader[0] = 0x09;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCCreateSuccessCodec.Deserialize(badHeader);
            });
        }
    }
}
