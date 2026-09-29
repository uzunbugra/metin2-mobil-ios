using System;
using System.Text;
using NUnit.Framework;
using Metin2.Protocol.Codecs;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class PacketCGLogin3Tests
    {
        [Test]
        public void PacketCGLogin3_Constants_MatchServerSource()
        {
            // Verified from source/Razuning-V5/Server/game/src/packet.h:516-522 and length.h:9-10
            Assert.AreEqual(111, PacketCGLogin3.PacketHeader);
            Assert.AreEqual(65, PacketCGLogin3.PacketSize);
            Assert.AreEqual(31, PacketCGLogin3.LoginBufferLen);
            Assert.AreEqual(17, PacketCGLogin3.PasswdBufferLen);
            Assert.AreEqual(4, PacketCGLogin3.KeyCount);
        }

        [Test]
        public void Serialize_ProducesExactGoldenBytes_WithDummyCredentials()
        {
            // Given dummy credentials (NO real secrets per AGENT_DEVELOPMENT_GUIDE § 0.1 rule 6)
            string dummyUser = "testuser";
            string dummyPass = "dummypass";
            uint[] dummyKeys = new uint[] { 0x01020304, 0x05060708, 0x090A0B0C, 0x0D0E0F10 };

            var packet = new PacketCGLogin3(dummyUser, dummyPass, dummyKeys);

            byte[] expectedGoldenBytes = new byte[65];
            expectedGoldenBytes[0] = 111; // Header (0x6f)

            // Fill login at [1..31]
            byte[] userBytes = Encoding.ASCII.GetBytes(dummyUser);
            Array.Copy(userBytes, 0, expectedGoldenBytes, 1, userBytes.Length);

            // Fill passwd at [32..48]
            byte[] passBytes = Encoding.ASCII.GetBytes(dummyPass);
            Array.Copy(passBytes, 0, expectedGoldenBytes, 32, passBytes.Length);

            // Fill keys at [49..64] (Little-Endian)
            // 0x01020304 -> 04 03 02 01
            expectedGoldenBytes[49] = 0x04;
            expectedGoldenBytes[50] = 0x03;
            expectedGoldenBytes[51] = 0x02;
            expectedGoldenBytes[52] = 0x01;

            // 0x05060708 -> 08 07 06 05
            expectedGoldenBytes[53] = 0x08;
            expectedGoldenBytes[54] = 0x07;
            expectedGoldenBytes[55] = 0x06;
            expectedGoldenBytes[56] = 0x05;

            // 0x090A0B0C -> 0C 0B 0A 09
            expectedGoldenBytes[57] = 0x0c;
            expectedGoldenBytes[58] = 0x0b;
            expectedGoldenBytes[59] = 0x0a;
            expectedGoldenBytes[60] = 0x09;

            // 0x0D0E0F10 -> 10 0F 0E 0D
            expectedGoldenBytes[61] = 0x10;
            expectedGoldenBytes[62] = 0x0f;
            expectedGoldenBytes[63] = 0x0e;
            expectedGoldenBytes[64] = 0x0d;

            // When:
            byte[] actualBytes = PacketCGLogin3Codec.Serialize(packet);

            // Then:
            Assert.AreEqual(65, actualBytes.Length);
            CollectionAssert.AreEqual(expectedGoldenBytes, actualBytes);
        }

        [Test]
        public void Deserialize_FromGoldenBytes_RestoresFieldsAccurately()
        {
            // Given: 65-byte golden buffer matching C++ TPacketCGLogin3 layout (packet.h:516-522)
            byte[] goldenBytes = new byte[65];
            goldenBytes[0] = 111; // Header (0x6F)

            // Fill login at [1..31]
            byte[] userBytes = Encoding.ASCII.GetBytes("metin2dev");
            Array.Copy(userBytes, 0, goldenBytes, 1, userBytes.Length);

            // Fill passwd at [32..48]
            byte[] passBytes = Encoding.ASCII.GetBytes("dummy123");
            Array.Copy(passBytes, 0, goldenBytes, 32, passBytes.Length);

            // Client Keys: 4 distinct, non-zero DWORDs in Little-Endian byte order
            // Key 0: 0x12345678 -> 78 56 34 12 (offsets 49..52)
            goldenBytes[49] = 0x78;
            goldenBytes[50] = 0x56;
            goldenBytes[51] = 0x34;
            goldenBytes[52] = 0x12;

            // Key 1: 0x9ABCDEF0 -> F0 DE BC 9A (offsets 53..56)
            goldenBytes[53] = 0xF0;
            goldenBytes[54] = 0xDE;
            goldenBytes[55] = 0xBC;
            goldenBytes[56] = 0x9A;

            // Key 2: 0x55AA33CC -> CC 33 AA 55 (offsets 57..60)
            goldenBytes[57] = 0xCC;
            goldenBytes[58] = 0x33;
            goldenBytes[59] = 0xAA;
            goldenBytes[60] = 0x55;

            // Key 3: 0xFEDCBA98 -> 98 BA DC FE (offsets 61..64)
            goldenBytes[61] = 0x98;
            goldenBytes[62] = 0xBA;
            goldenBytes[63] = 0xDC;
            goldenBytes[64] = 0xFE;

            // When:
            PacketCGLogin3 packet = PacketCGLogin3Codec.Deserialize(goldenBytes);

            // Then:
            Assert.AreEqual(111, packet.Header, "Header mismatch.");
            Assert.AreEqual("metin2dev", packet.Login, "Login string mismatch.");
            Assert.AreEqual("dummy123", packet.Password, "Password string mismatch.");

            Assert.IsNotNull(packet.ClientKeys, "ClientKeys array must not be null.");
            Assert.AreEqual(4, packet.ClientKeys.Length, "ClientKeys array must contain exactly 4 keys.");
            Assert.AreEqual(0x12345678u, packet.ClientKeys[0], "ClientKeys[0] mismatch.");
            Assert.AreEqual(0x9ABCDEF0u, packet.ClientKeys[1], "ClientKeys[1] mismatch.");
            Assert.AreEqual(0x55AA33CCu, packet.ClientKeys[2], "ClientKeys[2] mismatch.");
            Assert.AreEqual(0xFEDCBA98u, packet.ClientKeys[3], "ClientKeys[3] mismatch.");
        }

        [Test]
        public void RoundTrip_Fidelity()
        {
            var original = new PacketCGLogin3("player_one", "sample_pass", new uint[] { 100, 200, 300, 400 });

            byte[] serialized = PacketCGLogin3Codec.Serialize(original);
            Assert.AreEqual(65, serialized.Length);

            PacketCGLogin3 restored = PacketCGLogin3Codec.Deserialize(serialized);

            Assert.AreEqual(original.Header, restored.Header);
            Assert.AreEqual(original.Login, restored.Login);
            Assert.AreEqual(original.Password, restored.Password);
            CollectionAssert.AreEqual(original.ClientKeys, restored.ClientKeys);
        }

        [Test]
        public void OversizedCredentials_TruncateSafelyWithoutBufferOverflow()
        {
            // Login max chars = 30, Password max chars = 16
            string longLogin = new string('A', 50);
            string longPass = new string('B', 30);

            var packet = new PacketCGLogin3(longLogin, longPass, new uint[] { 1, 2, 3, 4 });
            byte[] serialized = PacketCGLogin3Codec.Serialize(packet);

            Assert.AreEqual(65, serialized.Length);

            PacketCGLogin3 restored = PacketCGLogin3Codec.Deserialize(serialized);

            Assert.AreEqual(30, restored.Login.Length);
            Assert.AreEqual(new string('A', 30), restored.Login);

            Assert.AreEqual(16, restored.Password.Length);
            Assert.AreEqual(new string('B', 16), restored.Password);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(32)]
        [TestCase(64)]
        public void Deserialize_TruncatedBuffer_ThrowsPacketUnderflowException(int length)
        {
            byte[] truncated = new byte[length];
            if (length > 0)
            {
                truncated[0] = 111;
            }

            Assert.Throws<PacketUnderflowException>(() =>
            {
                PacketCGLogin3Codec.Deserialize(truncated);
            });
        }

        [Test]
        public void Deserialize_InvalidHeader_ThrowsInvalidPacketHeaderException()
        {
            byte[] badHeader = new byte[65];
            badHeader[0] = 110; // Not 111

            Assert.Throws<InvalidPacketHeaderException>(() =>
            {
                PacketCGLogin3Codec.Deserialize(badHeader);
            });
        }
    }
}
