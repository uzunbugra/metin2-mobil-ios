using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGCharacterCreateTests
    {
        [Test]
        public void PacketCGCharacterCreate_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:543-554 (header packet.h:14):
            // 1+1+25+2+1+4 = 34.
            Assert.AreEqual(4, PacketCGCharacterCreate.PacketHeader);
            Assert.AreEqual(34, PacketCGCharacterCreate.PacketSize);
        }

        [Test]
        public void Serialize_ProducesExactWireLayout()
        {
            var packet = new PacketCGCharacterCreate(2, "Hero", 1, 0, 6, 4, 5, 3);
            byte[] bytes = PacketCGCharacterCreateCodec.Serialize(packet);

            Assert.AreEqual(34, bytes.Length);
            Assert.AreEqual(0x04, bytes[0]);
            Assert.AreEqual(0x02, bytes[1]);
            Assert.AreEqual((byte)'H', bytes[2]);
            Assert.AreEqual((byte)'o', bytes[5]);
            Assert.AreEqual(0x00, bytes[6]);
            // job u16 LE = 1 at [27..28].
            Assert.AreEqual(0x01, bytes[27]);
            Assert.AreEqual(0x00, bytes[28]);
            // shape/con/int/str/dex at [29..33].
            Assert.AreEqual(0x00, bytes[29]);
            Assert.AreEqual(0x06, bytes[30]);
            Assert.AreEqual(0x04, bytes[31]);
            Assert.AreEqual(0x05, bytes[32]);
            Assert.AreEqual(0x03, bytes[33]);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketCGCharacterCreate(1, "Ninja", 2, 1, 5, 6, 7, 8);
            PacketCGCharacterCreate restored =
                PacketCGCharacterCreateCodec.Deserialize(PacketCGCharacterCreateCodec.Serialize(original));

            Assert.AreEqual(1, restored.Index);
            Assert.AreEqual("Ninja", restored.Name);
            Assert.AreEqual(2, restored.Job);
            Assert.AreEqual(1, restored.Shape);
            Assert.AreEqual(5, restored.Con);
            Assert.AreEqual(6, restored.Int);
            Assert.AreEqual(7, restored.Str);
            Assert.AreEqual(8, restored.Dex);
        }

        [TestCase(0)]
        [TestCase(33)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x04;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGCharacterCreateCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[34];
            badHeader[0] = 0x05;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGCharacterCreateCodec.Deserialize(badHeader);
            });
        }
    }
}
