using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCLoginSuccessTests
    {
        [Test]
        public void PacketGCLoginSuccess_Constants_MatchServerSource()
        {
            // Server game/src/packet.h:838-847 (header packet.h:125):
            // 1 + 63*4 + 4*4 + 13*4 + 4 + 4 = 329.
            Assert.AreEqual(32, PacketGCLoginSuccess.PacketHeader);
            Assert.AreEqual(4, PacketGCLoginSuccess.SlotCount);
            Assert.AreEqual(329, PacketGCLoginSuccess.PacketSize);
        }

        private static PacketGCLoginSuccess SamplePacket()
        {
            var players = new SimplePlayer[4];
            players[0] = SimplePlayerCodecTests.SampleSlot();
            players[2] = new SimplePlayer
            {
                Id = 777,
                Name = "Shaman02",
                Job = 3,
                Level = 105,
                PlayMinutes = 123456,
                St = 50,
                Ht = 60,
                Dx = 70,
                Iq = 90,
                MainPart = 40200,
                ChangeName = 1,
                HairPart = 3100,
                X = 100,
                Y = 200,
                AddrNetworkOrder = 0x0100007Fu,
                Port = 13002,
                SkillGroup = 2
            };

            return new PacketGCLoginSuccess(
                players,
                new uint[] { 0, 555, 0, 0 },
                new string[] { string.Empty, "Knights", string.Empty, string.Empty },
                0x00ABCDEF,
                0x13572468);
        }

        [Test]
        public void Serialize_ProducesExactWireLayout()
        {
            byte[] bytes = PacketGCLoginSuccessCodec.Serialize(SamplePacket());

            Assert.AreEqual(329, bytes.Length);
            Assert.AreEqual(0x20, bytes[0]);
            // Slot 0 name at [5..]: "Warrior01".
            Assert.AreEqual((byte)'W', bytes[5]);
            // Slot 1 empty: id zero at [64..67].
            Assert.AreEqual(0x00, bytes[64]);
            Assert.AreEqual(0x00, bytes[67]);
            // Slot 2 id 777 = 0x309 at [127..130] LE.
            Assert.AreEqual(0x09, bytes[127]);
            Assert.AreEqual(0x03, bytes[128]);
            // guild_id[1] = 555 = 0x22B at [257..260] LE.
            Assert.AreEqual(0x2B, bytes[257]);
            Assert.AreEqual(0x02, bytes[258]);
            // guild_name[1] = "Knights" at [282..288].
            Assert.AreEqual((byte)'K', bytes[282]);
            Assert.AreEqual((byte)'s', bytes[288]);
            Assert.AreEqual(0x00, bytes[289]);
            // handle 0x00ABCDEF LE at [321..324].
            Assert.AreEqual(0xEF, bytes[321]);
            Assert.AreEqual(0xCD, bytes[322]);
            Assert.AreEqual(0xAB, bytes[323]);
            Assert.AreEqual(0x00, bytes[324]);
            // random_key 0x13572468 LE at [325..328].
            Assert.AreEqual(0x68, bytes[325]);
            Assert.AreEqual(0x24, bytes[326]);
            Assert.AreEqual(0x57, bytes[327]);
            Assert.AreEqual(0x13, bytes[328]);
        }

        [Test]
        public void RoundTrip_PreservesAllSlotsGuildsAndMark()
        {
            PacketGCLoginSuccess original = SamplePacket();
            PacketGCLoginSuccess restored =
                PacketGCLoginSuccessCodec.Deserialize(PacketGCLoginSuccessCodec.Serialize(original));

            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(original.Players[i].Equals(restored.Players[i]), $"Slot {i} mismatch.");
            }

            CollectionAssert.AreEqual(original.GuildIds, restored.GuildIds);
            CollectionAssert.AreEqual(original.GuildNames, restored.GuildNames);
            Assert.AreEqual(original.Handle, restored.Handle);
            Assert.AreEqual(original.RandomKey, restored.RandomKey);
        }

        [TestCase(0)]
        [TestCase(328)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0x20;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCLoginSuccessCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[329];
            badHeader[0] = 0x06; // legacy SUCCESS3, not NEWSLOT

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCLoginSuccessCodec.Deserialize(badHeader);
            });
        }
    }
}
