using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGCharacterSelectTests
    {
        [Test]
        public void PacketCGCharacterSelect_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:530-534 (header packet.h:16).
            Assert.AreEqual(6, PacketCGCharacterSelect.PacketHeader);
            Assert.AreEqual(2, PacketCGCharacterSelect.PacketSize);
        }

        [TestCase((byte)0)]
        [TestCase((byte)3)]
        public void RoundTrip_PreservesIndex(byte index)
        {
            byte[] bytes = PacketCGCharacterSelectCodec.Serialize(new PacketCGCharacterSelect(index));

            Assert.AreEqual(2, bytes.Length);
            Assert.AreEqual(0x06, bytes[0]);
            Assert.AreEqual(index, bytes[1]);

            PacketCGCharacterSelect restored = PacketCGCharacterSelectCodec.Deserialize(bytes);
            Assert.AreEqual(index, restored.Index);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x06;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGCharacterSelectCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGCharacterSelectCodec.Deserialize(new byte[] { 0x04, 0x00 });
            });
        }
    }
}
