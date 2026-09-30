using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class CamelliaEngineTests
    {
        // KAT sources: RFC 3713 Appendix A (128/192/256-bit) + NESSIE
        // submission via CryptoPP TestVectors/camellia.txt (zero key).
        [TestCase(
            "0123456789abcdeffedcba9876543210",
            "0123456789abcdeffedcba9876543210",
            "67673138549669730857065648eabe43")]
        [TestCase(
            "0123456789abcdeffedcba98765432100011223344556677",
            "0123456789abcdeffedcba9876543210",
            "b4993401b3e996f84ee5cee7d79b09b9")]
        [TestCase(
            "0123456789abcdeffedcba987654321000112233445566778899aabbccddeeff",
            "0123456789abcdeffedcba9876543210",
            "9acc237dff16d76c20ef7c919e3a7509")]
        [TestCase(
            "00000000000000000000000000000000",
            "00000000000000000000000000000000",
            "3d028025b156327c17f762c1f2cbca71")]
        public void EncryptBlock_MatchesCamelliaKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new CamelliaEngine(FromHex(keyHex));
            byte[] keystream = new byte[16];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesKAT()
        {
            var engine = new CamelliaEngine(FromHex("0123456789abcdeffedcba987654321000112233445566778899aabbccddeeff"));
            byte[] output = new byte[16];

            engine.DecryptBlock(FromHex("9acc237dff16d76c20ef7c919e3a7509"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("0123456789abcdeffedcba9876543210"), output);
        }

        [TestCase("0123456789abcdeffedcba9876543210")]
        [TestCase("0123456789abcdeffedcba98765432100011223344556677")]
        [TestCase("0123456789abcdeffedcba987654321000112233445566778899aabbccddeeff")]
        public void RoundTrip_RestoresPlaintext(string keyHex)
        {
            var engine = new CamelliaEngine(FromHex(keyHex));
            byte[] block = FromHex("FEDCBA98765432100011223344556677");
            byte[] encrypted = new byte[16];
            byte[] decrypted = new byte[16];

            engine.EncryptBlock(block, 0, encrypted, 0);
            Assert.AreNotEqual(block, encrypted);

            engine.DecryptBlock(encrypted, 0, decrypted, 0);
            CollectionAssert.AreEqual(block, decrypted);
        }

        [Test]
        public void BlockSize_IsSixteen()
        {
            Assert.AreEqual(16, new CamelliaEngine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new CamelliaEngine(null));
            Assert.Throws<System.ArgumentException>(() => new CamelliaEngine(new byte[8]));
            Assert.Throws<System.ArgumentException>(() => new CamelliaEngine(new byte[15]));
            Assert.Throws<System.ArgumentException>(() => new CamelliaEngine(new byte[17]));
            Assert.Throws<System.ArgumentException>(() => new CamelliaEngine(new byte[20]));
            Assert.Throws<System.ArgumentException>(() => new CamelliaEngine(new byte[31]));
            Assert.Throws<System.ArgumentException>(() => new CamelliaEngine(new byte[33]));
            Assert.DoesNotThrow(() => new CamelliaEngine(new byte[16]));
            Assert.DoesNotThrow(() => new CamelliaEngine(new byte[24]));
            Assert.DoesNotThrow(() => new CamelliaEngine(new byte[32]));
        }

        private static byte[] FromHex(string hex)
        {
            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return result;
        }
    }
}
