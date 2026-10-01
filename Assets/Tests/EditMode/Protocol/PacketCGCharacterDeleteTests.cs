using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGCharacterDeleteTests
    {
        [Test]
        public void PacketCGCharacterDelete_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:536-541 (header packet.h:15): 1+1+8 = 10.
            Assert.AreEqual(5, PacketCGCharacterDelete.PacketHeader);
            Assert.AreEqual(10, PacketCGCharacterDelete.PacketSize);
        }

        [Test]
        public void Serialize_ProducesExactWireLayout()
        {
            byte[] bytes = PacketCGCharacterDeleteCodec.Serialize(new PacketCGCharacterDelete(1, "1234567"));

            Assert.AreEqual(10, bytes.Length);
            Assert.AreEqual(0x05, bytes[0]);
            Assert.AreEqual(0x01, bytes[1]);
            Assert.AreEqual((byte)'1', bytes[2]);
            Assert.AreEqual((byte)'7', bytes[8]);
            Assert.AreEqual(0x00, bytes[9]);
        }

        [Test]
        public void RoundTrip_PreservesIndexAndCode()
        {
            var original = new PacketCGCharacterDelete(3, "0000000");
            PacketCGCharacterDelete restored =
                PacketCGCharacterDeleteCodec.Deserialize(PacketCGCharacterDeleteCodec.Serialize(original));

            Assert.AreEqual(3, restored.Index);
            Assert.AreEqual("0000000", restored.PrivateCode);
        }

        [TestCase(0)]
        [TestCase(9)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x05;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGCharacterDeleteCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[10];
            badHeader[0] = 0x06;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGCharacterDeleteCodec.Deserialize(badHeader);
            });
        }
    }
}
