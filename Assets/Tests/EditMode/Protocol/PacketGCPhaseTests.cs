using System;
using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCPhaseTests
    {
        [Test]
        public void PacketGCPhase_Constants_MatchServerSource()
        {
            // Verified from source/Razuning-V5/Server/game/src/packet.h:814-818
            Assert.AreEqual(0xfd, PacketGCPhase.PacketHeader);
            Assert.AreEqual(2, PacketGCPhase.PacketSize);
        }

        [TestCase(PhaseType.Close, (byte)0)]
        [TestCase(PhaseType.Handshake, (byte)1)]
        [TestCase(PhaseType.Login, (byte)2)]
        [TestCase(PhaseType.Select, (byte)3)]
        [TestCase(PhaseType.Loading, (byte)4)]
        [TestCase(PhaseType.Game, (byte)5)]
        [TestCase(PhaseType.Dead, (byte)6)]
        [TestCase(PhaseType.ClientConnecting, (byte)7)]
        [TestCase(PhaseType.DbClient, (byte)8)]
        [TestCase(PhaseType.P2P, (byte)9)]
        [TestCase(PhaseType.Auth, (byte)10)]
        [TestCase(PhaseType.Teen, (byte)11)]
        public void Serialize_ProducesExactGoldenBytes_ForPhase(PhaseType phase, byte expectedPhaseByte)
        {
            var packet = new PacketGCPhase(phase);
            byte[] bytes = PacketGCPhaseCodec.Serialize(packet);

            Assert.AreEqual(2, bytes.Length);
            Assert.AreEqual(0xfd, bytes[0], "Header must be 0xfd (HEADER_GC_PHASE)");
            Assert.AreEqual(expectedPhaseByte, bytes[1], $"Phase byte for {phase} must be {expectedPhaseByte}");
        }

        [TestCase((byte)0, PhaseType.Close)]
        [TestCase((byte)1, PhaseType.Handshake)]
        [TestCase((byte)2, PhaseType.Login)]
        [TestCase((byte)3, PhaseType.Select)]
        [TestCase((byte)4, PhaseType.Loading)]
        [TestCase((byte)5, PhaseType.Game)]
        [TestCase((byte)6, PhaseType.Dead)]
        [TestCase((byte)7, PhaseType.ClientConnecting)]
        [TestCase((byte)8, PhaseType.DbClient)]
        [TestCase((byte)9, PhaseType.P2P)]
        [TestCase((byte)10, PhaseType.Auth)]
        [TestCase((byte)11, PhaseType.Teen)]
        public void Deserialize_FromGoldenBytes_RestoresPhaseType(byte phaseByte, PhaseType expectedPhase)
        {
            byte[] goldenBytes = new byte[] { 0xfd, phaseByte };
            PacketGCPhase packet = PacketGCPhaseCodec.Deserialize(goldenBytes);

            Assert.AreEqual(0xfd, packet.Header);
            Assert.AreEqual(expectedPhase, packet.Phase);
        }

        [Test]
        public void RoundTrip_AllEnumValues()
        {
            foreach (PhaseType phase in Enum.GetValues(typeof(PhaseType)))
            {
                var original = new PacketGCPhase(phase);
                byte[] bytes = PacketGCPhaseCodec.Serialize(original);
                PacketGCPhase restored = PacketGCPhaseCodec.Deserialize(bytes);

                Assert.AreEqual(original.Header, restored.Header);
                Assert.AreEqual(original.Phase, restored.Phase);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0xfd;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCPhaseCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[] { 0xfc, 0x01 };

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCPhaseCodec.Deserialize(badHeader);
            });
        }
    }
}
