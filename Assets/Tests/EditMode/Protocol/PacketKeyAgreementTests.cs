using System;
using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketKeyAgreementTests
    {
        [Test]
        public void PacketKeyAgreement_Constants_MatchServerSource()
        {
            // Verified from source/Razuning-V5/Server/game/src/packet.h:2226-2233
            Assert.AreEqual(0xfb, PacketKeyAgreement.PacketHeader);
            Assert.AreEqual(261, PacketKeyAgreement.PacketSize);
            Assert.AreEqual(256, PacketKeyAgreement.MaxDataLen);
        }

        [Test]
        public void Serialize_ProducesExactGoldenBytes_LittleEndian()
        {
            // Given:
            // Header = 0xfb
            // wAgreedLength = 128 = 0x0080 (LE: 80 00)
            // wDataLength = 4 = 0x0004 (LE: 04 00)
            // data = 4 bytes { 0xAA, 0xBB, 0xCC, 0xDD } zero padded to 256 bytes
            byte[] payload = new byte[256];
            payload[0] = 0xAA;
            payload[1] = 0xBB;
            payload[2] = 0xCC;
            payload[3] = 0xDD;

            var packet = new PacketKeyAgreement(128, 4, payload);

            byte[] expectedGoldenBytes = new byte[261];
            expectedGoldenBytes[0] = 0xfb;       // [0] Header (251)
            expectedGoldenBytes[1] = 0x80;       // [1..2] wAgreedLength = 128 LE
            expectedGoldenBytes[2] = 0x00;
            expectedGoldenBytes[3] = 0x04;       // [3..4] wDataLength = 4 LE
            expectedGoldenBytes[4] = 0x00;
            expectedGoldenBytes[5] = 0xAA;       // [5..260] data[256]
            expectedGoldenBytes[6] = 0xBB;
            expectedGoldenBytes[7] = 0xCC;
            expectedGoldenBytes[8] = 0xDD;

            // When:
            byte[] actualBytes = PacketKeyAgreementCodec.Serialize(packet);

            // Then:
            Assert.AreEqual(261, actualBytes.Length);
            CollectionAssert.AreEqual(expectedGoldenBytes, actualBytes);
        }

        [Test]
        public void Serialize_PartialData_ZeroPadsRemainingBytes()
        {
            // Given only 3 bytes of data instead of 256
            byte[] shortData = new byte[] { 0x11, 0x22, 0x33 };
            var packet = new PacketKeyAgreement(64, 3, shortData);

            byte[] serialized = PacketKeyAgreementCodec.Serialize(packet);
            Assert.AreEqual(261, serialized.Length);
            Assert.AreEqual(0xfb, serialized[0]);
            Assert.AreEqual(64, serialized[1]); // LE 64
            Assert.AreEqual(0, serialized[2]);
            Assert.AreEqual(3, serialized[3]);  // LE 3
            Assert.AreEqual(0, serialized[4]);

            // Data payload: first 3 bytes are non-zero, remainder must be 0x00
            Assert.AreEqual(0x11, serialized[5]);
            Assert.AreEqual(0x22, serialized[6]);
            Assert.AreEqual(0x33, serialized[7]);
            for (int i = 8; i < 261; i++)
            {
                Assert.AreEqual(0x00, serialized[i], $"Byte at index {i} must be zero padded.");
            }
        }

        [Test]
        public void Deserialize_FromGoldenBytes_RestoresAllFields()
        {
            byte[] goldenBytes = new byte[261];
            goldenBytes[0] = 0xfb;
            goldenBytes[1] = 0x80;
            goldenBytes[2] = 0x00;
            goldenBytes[3] = 0x04;
            goldenBytes[4] = 0x00;
            goldenBytes[5] = 0xAA;
            goldenBytes[6] = 0xBB;
            goldenBytes[7] = 0xCC;
            goldenBytes[8] = 0xDD;

            PacketKeyAgreement packet = PacketKeyAgreementCodec.Deserialize(goldenBytes);

            Assert.AreEqual(0xfb, packet.Header);
            Assert.AreEqual(128, packet.AgreedLength);
            Assert.AreEqual(4, packet.DataLength);
            Assert.AreEqual(256, packet.Data.Length);
            Assert.AreEqual(0xAA, packet.Data[0]);
            Assert.AreEqual(0xBB, packet.Data[1]);
            Assert.AreEqual(0xCC, packet.Data[2]);
            Assert.AreEqual(0xDD, packet.Data[3]);
            Assert.AreEqual(0x00, packet.Data[4]);
        }

        [Test]
        public void RoundTrip_Fidelity()
        {
            byte[] testData = new byte[256];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)(i ^ 0x5A);
            }

            var original = new PacketKeyAgreement(256, 256, testData);
            byte[] serialized = PacketKeyAgreementCodec.Serialize(original);
            Assert.AreEqual(261, serialized.Length);

            PacketKeyAgreement restored = PacketKeyAgreementCodec.Deserialize(serialized);

            Assert.AreEqual(original.Header, restored.Header);
            Assert.AreEqual(original.AgreedLength, restored.AgreedLength);
            Assert.AreEqual(original.DataLength, restored.DataLength);
            CollectionAssert.AreEqual(original.Data, restored.Data);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(260)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 0xfb;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketKeyAgreementCodec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[261];
            badHeader[0] = 0xfa; // Not 0xfb

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketKeyAgreementCodec.Deserialize(badHeader);
            });
        }
    }
}
