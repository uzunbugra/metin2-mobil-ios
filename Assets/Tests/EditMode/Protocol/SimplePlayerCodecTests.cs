using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class SimplePlayerCodecTests
    {
        [Test]
        public void SimplePlayer_FieldSize_MatchesSourceStruct()
        {
            // tables.h:275-291 under #pragma pack(1), name len 24+1:
            // 4+25+1+1+4+4+2+1+2+4+4+4+4+2+1 = 63.
            Assert.AreEqual(63, SimplePlayer.FieldSize);
        }

        public static SimplePlayer SampleSlot()
        {
            return new SimplePlayer
            {
                Id = 12345,
                Name = "Warrior01",
                Job = 0,
                Level = 35,
                PlayMinutes = 9876,
                St = 90,
                Ht = 80,
                Dx = 70,
                Iq = 60,
                MainPart = 40119,
                ChangeName = 0,
                HairPart = 3005,
                X = 474387,
                Y = 954234,
                // 127.0.0.1 in network order (inet_addr output).
                AddrNetworkOrder = 0x0100007Fu,
                Port = 13001,
                SkillGroup = 1
            };
        }

        [Test]
        public void Serialize_ProducesExactFieldLayout()
        {
            byte[] buffer = new byte[SimplePlayer.FieldSize];
            int written = SimplePlayerCodec.Serialize(SampleSlot(), buffer);

            Assert.AreEqual(63, written);
            // dwID LE.
            Assert.AreEqual(0x39, buffer[0]);
            Assert.AreEqual(0x30, buffer[1]);
            // szName.
            Assert.AreEqual((byte)'W', buffer[4]);
            Assert.AreEqual((byte)'1', buffer[12]);
            Assert.AreEqual(0x00, buffer[13]);
            // byJob/byLevel.
            Assert.AreEqual(0x00, buffer[29]);
            Assert.AreEqual(35, buffer[30]);
            // dwPlayMinutes LE = 9876 = 0x2694.
            Assert.AreEqual(0x94, buffer[31]);
            Assert.AreEqual(0x26, buffer[32]);
            // Stats.
            Assert.AreEqual(90, buffer[35]);
            Assert.AreEqual(80, buffer[36]);
            Assert.AreEqual(70, buffer[37]);
            Assert.AreEqual(60, buffer[38]);
            // wMainPart LE = 40119 = 0x9CB7.
            Assert.AreEqual(0xB7, buffer[39]);
            Assert.AreEqual(0x9C, buffer[40]);
            // bDummy zeroed.
            Assert.AreEqual(0x00, buffer[44]);
            Assert.AreEqual(0x00, buffer[47]);
            // wPort LE = 13001 = 0x32C9 at [60..61].
            Assert.AreEqual(0xC9, buffer[60]);
            Assert.AreEqual(0x32, buffer[61]);
            // skillGroup at [62].
            Assert.AreEqual(0x01, buffer[62]);
        }

        [Test]
        public void RoundTrip_PreservesEveryField()
        {
            SimplePlayer original = SampleSlot();
            byte[] buffer = new byte[SimplePlayer.FieldSize];
            SimplePlayerCodec.Serialize(original, buffer);

            Assert.IsTrue(SimplePlayerCodec.TryDeserialize(buffer, out SimplePlayer restored, out string _));
            Assert.IsTrue(original.Equals(restored));
            Assert.AreEqual(original.GetHashCode(), restored.GetHashCode());
        }

        [Test]
        public void TryDeserialize_Truncated_ReturnsFalse()
        {
            Assert.IsFalse(SimplePlayerCodec.TryDeserialize(
                new byte[62], out SimplePlayer _, out string error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void RoundTrip_EmptySlot_AllZeros()
        {
            var empty = new SimplePlayer { Name = string.Empty };
            byte[] buffer = new byte[SimplePlayer.FieldSize];
            SimplePlayerCodec.Serialize(empty, buffer);

            Assert.IsTrue(SimplePlayerCodec.TryDeserialize(buffer, out SimplePlayer restored, out string _));
            Assert.AreEqual(0u, restored.Id);
            Assert.AreEqual(string.Empty, restored.Name);
        }
    }
}
