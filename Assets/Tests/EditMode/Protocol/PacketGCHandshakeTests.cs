using System;
using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketGCHandshakeTests
    {
        [Test]
        public void PacketGCHandshake_Constants_MatchServerSource()
        {
            // Verified from source/Razuning-V5/Server/game/src/packet.h:789-795
            Assert.AreEqual(0xff, PacketGCHandshake.PacketHeader);
            Assert.AreEqual(13, PacketGCHandshake.PacketSize);
        }

        [Test]
        public void Serialize_ProducesExactGoldenBytes_LittleEndian()
        {
            // Given:
            // Header = 0xff
            // dwHandshake = 0x12345678 (LE: 78 56 34 12)
            // dwTime = 0x0001E240 = 123456 ms (LE: 40 E2 01 00)
            // lDelta = 50 = 0x00000032 (LE: 32 00 00 00)
            var packet = new PacketGCHandshake(0x12345678, 123456, 50);

            byte[] expectedGoldenBytes = new byte[]
            {
                0xff,                   // [0] Header (255)
                0x78, 0x56, 0x34, 0x12, // [1..4] dwHandshake
                0x40, 0xe2, 0x01, 0x00, // [5..8] dwTime
                0x32, 0x00, 0x00, 0x00  // [9..12] lDelta
            };

            // When:
            byte[] actualBytes = PacketGCHandshakeCodec.Serialize(packet);

            // Then:
            Assert.AreEqual(13, actualBytes.Length);
            CollectionAssert.AreEqual(expectedGoldenBytes, actualBytes, "Serialized bytes must match golden byte representation.");
        }

        [Test]
        public void Deserialize_FromGoldenBytes_RestoresAllFields()
        {
            // Given:
            byte[] goldenBytes = new byte[]
            {
                0xff,
                0x78, 0x56, 0x34, 0x12,
                0x40, 0xe2, 0x01, 0x00,
                0x32, 0x00, 0x00, 0x00
            };

            // When:
            PacketGCHandshake packet = PacketGCHandshakeCodec.Deserialize(goldenBytes);

            // Then:
            Assert.AreEqual(0xff, packet.Header);
            Assert.AreEqual(0x12345678u, packet.Handshake);
            Assert.AreEqual(123456u, packet.Time);
            Assert.AreEqual(50, packet.Delta);
        }

        [Test]
        public void RoundTrip_ArbitraryValues_MaintainsFidelity()
        {
            var original = new PacketGCHandshake(0xDEADBEEF, 987654321, -120);

            byte[] buffer = new byte[PacketGCHandshake.PacketSize];
            int written = PacketGCHandshakeCodec.Serialize(original, buffer);
            Assert.AreEqual(13, written);

            PacketGCHandshake restored = PacketGCHandshakeCodec.Deserialize(buffer);

            Assert.AreEqual(original.Header, restored.Header);
            Assert.AreEqual(original.Handshake, restored.Handshake);
            Assert.AreEqual(original.Time, restored.Time);
            Assert.AreEqual(original.Delta, restored.Delta);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(12)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0xff;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketGCHandshakeCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Serialize_BufferTooSmall_ThrowsArgumentException()
        {
            var packet = new PacketGCHandshake(1, 2, 3);
            byte[] smallBuffer = new byte[12];

            Assert.Throws<ArgumentException>(() =>
            {
                PacketGCHandshakeCodec.Serialize(packet, smallBuffer);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeaderBytes = new byte[13];
            badHeaderBytes[0] = 0xfe; // Instead of 0xff

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketGCHandshakeCodec.Deserialize(badHeaderBytes);
            });
        }
    }
}
