using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class SeedEngineTests
    {
        // KAT sources: RFC 4269 via CryptoPP TestVectors/seed.txt (fixed 16-byte key).
        [TestCase(
            "00000000000000000000000000000000",
            "000102030405060708090A0B0C0D0E0F",
            "5EBAC6E0054E166819AFF1CC6D346CDB")]
        [TestCase(
            "000102030405060708090A0B0C0D0E0F",
            "00000000000000000000000000000000",
            "C11F22F20140505084483597E4370F43")]
        [TestCase(
            "4706480851E61BE85D74BFB3FD956185",
            "83A2F8A288641FB9A4E9A5CC2F131C7D",
            "EE54D13EBCAE706D226BC3142CD40D4A")]
        [TestCase(
            "28DBC3BC49FFD87DCFA509B11D422BE7",
            "B41E6BE2EBA84A148E2EED84593C5EC7",
            "9B9B7BFCD1813CB95D0B3618F40F5122")]
        public void EncryptBlock_MatchesSeedKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new SeedEngine(FromHex(keyHex));
            byte[] keystream = new byte[16];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesKAT()
        {
            var engine = new SeedEngine(FromHex("4706480851E61BE85D74BFB3FD956185"));
            byte[] output = new byte[16];

            engine.DecryptBlock(FromHex("EE54D13EBCAE706D226BC3142CD40D4A"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("83A2F8A288641FB9A4E9A5CC2F131C7D"), output);
        }

        [TestCase("00000000000000000000000000000000")]
        [TestCase("4706480851E61BE85D74BFB3FD956185")]
        public void RoundTrip_RestoresPlaintext(string keyHex)
        {
            var engine = new SeedEngine(FromHex(keyHex));
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
            Assert.AreEqual(16, new SeedEngine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new SeedEngine(null));
            Assert.Throws<System.ArgumentException>(() => new SeedEngine(new byte[8]));
            Assert.Throws<System.ArgumentException>(() => new SeedEngine(new byte[15]));
            Assert.Throws<System.ArgumentException>(() => new SeedEngine(new byte[17]));
            Assert.Throws<System.ArgumentException>(() => new SeedEngine(new byte[24]));
            Assert.Throws<System.ArgumentException>(() => new SeedEngine(new byte[32]));
            Assert.DoesNotThrow(() => new SeedEngine(new byte[16]));
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
