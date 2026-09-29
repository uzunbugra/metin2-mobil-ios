using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class TeaEngineTests
    {
        // KAT sources: Wheeler–Needham original paper (zero vector) and the independent
        // Needham/Wheeler-derived vector set (catacomb tests). Words are big-endian.
        [TestCase("00000000000000000000000000000000", "0000000000000000", "41EA3A0A94BAA940")]
        [TestCase("00000000000000000000000000000000", "0000000100000001", "E0050D074FB50C13")]
        [TestCase("00000000000000000000000000000000", "123456789ABCDEF0", "7FE2E4804F66BD75")]
        [TestCase("00000000000000000000000000000000", "FFFFFFFFFFFFFFFF", "F6F4BF6E1335B5B8")]
        [TestCase("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", "319BBEFB016ABDB2")]
        [TestCase("123456789ABCDEF0123456789ABCDEF0", "0000000000000000", "BCDA87371024D312")]
        [TestCase("123456789ABCDEF0123456789ABCDEF0", "0000000100000001", "8AC711A075CFE57E")]
        public void EncryptBlock_MatchesPublishedKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new TeaEngine(FromHex(keyHex));
            byte[] counter = FromHex(plainHex);
            byte[] keystream = new byte[8];

            engine.EncryptBlock(counter, 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [TestCase("00000000000000000000000000000000", "41EA3A0A94BAA940", "0000000000000000")]
        [TestCase("123456789ABCDEF0123456789ABCDEF0", "BCDA87371024D312", "0000000000000000")]
        [TestCase("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "319BBEFB016ABDB2", "FFFFFFFFFFFFFFFF")]
        public void DecryptBlock_ReversesKAT(string keyHex, string cipherHex, string expectedHex)
        {
            var engine = new TeaEngine(FromHex(keyHex));
            byte[] output = new byte[8];

            engine.DecryptBlock(FromHex(cipherHex), 0, output, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), output);
        }

        [Test]
        public void RoundTrip_NonTrivialKey_RestoresPlaintext()
        {
            var engine = new TeaEngine(FromHex("00112233445566778899AABBCCDDEEFF"));
            byte[] block = FromHex("0123456789ABCDEF");
            byte[] encrypted = new byte[8];
            byte[] decrypted = new byte[8];

            engine.EncryptBlock(block, 0, encrypted, 0);
            Assert.AreNotEqual(block, encrypted);

            engine.DecryptBlock(encrypted, 0, decrypted, 0);
            CollectionAssert.AreEqual(block, decrypted);
        }

        [Test]
        public void BlockSize_IsEight()
        {
            Assert.AreEqual(8, new TeaEngine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new TeaEngine(null));
            Assert.Throws<System.ArgumentException>(() => new TeaEngine(new byte[15]));
            Assert.Throws<System.ArgumentException>(() => new TeaEngine(new byte[17]));
        }

        [Test]
        public void BlockOffsets_AreHonored()
        {
            var engine = new TeaEngine(FromHex("00000000000000000000000000000000"));
            byte[] input = new byte[] { 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF };
            byte[] output = new byte[12];

            engine.EncryptBlock(input, 2, output, 2);

            Assert.AreEqual(0, output[0]);
            Assert.AreEqual(0, output[1]);
            Assert.AreEqual(0x00, output[11]);
            CollectionAssert.AreEqual(
                FromHex("41EA3A0A94BAA940"),
                new byte[] { output[2], output[3], output[4], output[5], output[6], output[7], output[8], output[9] });
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
