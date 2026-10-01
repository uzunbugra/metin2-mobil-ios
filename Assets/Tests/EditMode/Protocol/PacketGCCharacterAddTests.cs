using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCCharacterAddTests
    {
        [Test]
        public void PacketGCCharacterAdd_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:886-903:
            // 1+4+4+12+1+2+1+1+1+8 = 35.
            Assert.AreEqual(1, PacketGCCharacterAdd.PacketHeader);
            Assert.AreEqual(35, PacketGCCharacterAdd.PacketSize);
        }

        [Test]
        public void Serialize_ProducesExactWireLayout()
        {
            var packet = new PacketGCCharacterAdd
            {
                Vid = 0x01020304,
                Angle = 1.5f, // 0x3FC00000
                X = -100,
                Y = 200,
                Z = 0,
                Type = 0,
                Race = 7,
                MovingSpeed = 150,
                AttackSpeed = 140,
                StateFlag = 1,
                AffectFlag0 = 0xAAAAAAAAu,
                AffectFlag1 = 0
            };
            byte[] bytes = PacketGCCharacterAddCodec.Serialize(packet);

            Assert.AreEqual(35, bytes.Length);
            Assert.AreEqual(0x01, bytes[0]);
            // vid LE at [1..4].
            Assert.AreEqual(0x04, bytes[1]);
            Assert.AreEqual(0x01, bytes[4]);
            // angle 1.5f = 00 00 C0 3F at [5..8].
            CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0xC0, 0x3F }, bytes[5..9]);
            // x = -100 = 0xFFFFFF9C at [9..12].
            CollectionAssert.AreEqual(new byte[] { 0x9C, 0xFF, 0xFF, 0xFF }, bytes[9..13]);
            // race LE = 7 at [22..23].
            Assert.AreEqual(0x07, bytes[22]);
            Assert.AreEqual(0x00, bytes[23]);
            // affect0 = 0xAAAAAAAA at [27..30].
            Assert.AreEqual(0xAA, bytes[27]);
            Assert.AreEqual(0xAA, bytes[30]);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketGCCharacterAdd
            {
                Vid = 987654,
                Angle = 3.14159f,
                X = 474387,
                Y = 954234,
                Z = -50,
                Type = 2,
                Race = 3001,
                MovingSpeed = 200,
                AttackSpeed = 180,
                StateFlag = 5,
                AffectFlag0 = 123,
                AffectFlag1 = 456
            };
            PacketGCCharacterAdd restored =
                PacketGCCharacterAddCodec.Deserialize(PacketGCCharacterAddCodec.Serialize(original));

            Assert.AreEqual(original.Vid, restored.Vid);
            Assert.AreEqual(original.Angle, restored.Angle);
            Assert.AreEqual(original.X, restored.X);
            Assert.AreEqual(original.Y, restored.Y);
            Assert.AreEqual(original.Z, restored.Z);
            Assert.AreEqual(original.Type, restored.Type);
            Assert.AreEqual(original.Race, restored.Race);
            Assert.AreEqual(original.MovingSpeed, restored.MovingSpeed);
            Assert.AreEqual(original.AttackSpeed, restored.AttackSpeed);
            Assert.AreEqual(original.StateFlag, restored.StateFlag);
            Assert.AreEqual(original.AffectFlag0, restored.AffectFlag0);
            Assert.AreEqual(original.AffectFlag1, restored.AffectFlag1);
        }

        [TestCase(0)]
        [TestCase(34)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x01;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCCharacterAddCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[35];
            badHeader[0] = 0x02;

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCCharacterAddCodec.Deserialize(badHeader);
            });
        }
    }
}
