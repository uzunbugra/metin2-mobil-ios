using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCPointChangeTests
    {
        [Test]
        public void Constants_MatchSource()
        {
            // Server packet.h:139 (HEADER_GC_CHARACTER_POINT_CHANGE) and client
            // Packet.h:160 (HEADER_GC_PLAYER_POINT_CHANGE) — both value 17;
            // struct = 4+4+1+4+4 = 17 (packet.h:1042-1049, Packet.h:1606-1615).
            Assert.AreEqual(17, PacketGCPointChange.PacketHeader);
            Assert.AreEqual(17, PacketGCPointChange.PacketSize);
        }

        [Test]
        public void Serialize_HpDelta_GoldenBytesWithIntHeaderQuirk()
        {
            // POINT_HP = 5 (char.h:104), amount -150, value 850:
            // int header 17 = 11 00 00 00 — three interior zero bytes follow the
            // 1-byte header value on the wire.
            byte[] bytes = PacketGCPointChangeCodec.Serialize(
                new PacketGCPointChange(0x11223344, PointTypes.Hp, -150, 850));
            CollectionAssert.AreEqual(
                new byte[]
                {
                    0x11, 0x00, 0x00, 0x00,             // int32 header (17)
                    0x44, 0x33, 0x22, 0x11,             // DWORD vid
                    0x05,                                // BYTE type (POINT_HP)
                    0x6A, 0xFF, 0xFF, 0xFF,             // int32 amount (-150 = 0xFFFFFF6A)
                    0x52, 0x03, 0x00, 0x00              // int32 value (850)
                },
                bytes);
        }

        [Test]
        public void Serialize_GoldDelta_ZeroAmountWhenNotSent()
        {
            byte[] bytes = PacketGCPointChangeCodec.Serialize(
                new PacketGCPointChange(9, PointTypes.Gold, 0, 12345));
            CollectionAssert.AreEqual(
                new byte[]
                {
                    0x11, 0x00, 0x00, 0x00,
                    0x09, 0x00, 0x00, 0x00,
                    0x0B,                                // POINT_GOLD = 11
                    0x00, 0x00, 0x00, 0x00,
                    0x39, 0x30, 0x00, 0x00              // 12345 = 0x3039
                },
                bytes);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PacketGCPointChange(0xDEADBEEF, PointTypes.Sp, -42, 1000);
            byte[] bytes = PacketGCPointChangeCodec.Serialize(original);
            PacketGCPointChange restored = PacketGCPointChangeCodec.Deserialize(bytes);

            Assert.AreEqual(original.Vid, restored.Vid);
            Assert.AreEqual(original.Type, restored.Type);
            Assert.AreEqual(original.Amount, restored.Amount);
            Assert.AreEqual(original.Value, restored.Value);
        }

        [TestCase(0)]
        [TestCase(8)]
        [TestCase(16)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCPointChangeCodec.Deserialize(new byte[length]);
            });
        }

        [Test]
        public void Deserialize_IntHeaderNot17_Fails()
        {
            // Header as int 18 in the first 4 bytes — must be rejected.
            byte[] wrong = PacketGCPointChangeCodec.Serialize(
                new PacketGCPointChange(1, 2, 3, 4));
            wrong[0] = 0x12;
            Assert.IsFalse(PacketGCPointChangeCodec.TryDeserialize(wrong, out _, out string error));
            StringAssert.Contains("header", error);
        }

        [Test]
        public void PointTypes_MatchSourceEnum()
        {
            // Spot-check EPointTypes (char.h:97-135).
            Assert.AreEqual(5, PointTypes.Hp);
            Assert.AreEqual(7, PointTypes.Sp);
            Assert.AreEqual(1, PointTypes.Level);
            Assert.AreEqual(3, PointTypes.Exp);
            Assert.AreEqual(11, PointTypes.Gold);
        }
    }
}
