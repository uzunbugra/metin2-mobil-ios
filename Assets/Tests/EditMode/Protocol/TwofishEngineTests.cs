using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class TwofishEngineTests
    {
        // KAT sources: Botan src/tests/data/block/twofish.vec (derived from the
        // AES submission test vectors). Chained triples: each vector's key is the
        // previous vector's ciphertext, so every KAT exercises a FRESH key
        // schedule — strong schedule coverage. All 128-bit (wire size) below.
        [TestCase(
            "00000000000000000000000000000000",
            "00000000000000000000000000000000",
            "9F589F5CF6122C32B6BFEC2F2AE8C35A")]
        [TestCase(
            "9F589F5CF6122C32B6BFEC2F2AE8C35A",
            "D491DB16E7B1C39E86CB086B789F5419",
            "019F9809DE1711858FAAC3A3BA20FBC3")]
        [TestCase(
            "D491DB16E7B1C39E86CB086B789F5419",
            "019F9809DE1711858FAAC3A3BA20FBC3",
            "6363977DE839486297E661C6C9D668EB")]
        [TestCase(
            "019F9809DE1711858FAAC3A3BA20FBC3",
            "6363977DE839486297E661C6C9D668EB",
            "816D5BD0FAE35342BF2A7412C246F752")]
        [TestCase(
            "6363977DE839486297E661C6C9D668EB",
            "816D5BD0FAE35342BF2A7412C246F752",
            "5449ECA008FF5921155F598AF4CED4D0")]
        public void EncryptBlock_MatchesBotanKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new TwofishEngine(FromHex(keyHex));
            byte[] keystream = new byte[16];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesKAT()
        {
            var engine = new TwofishEngine(FromHex("9F589F5CF6122C32B6BFEC2F2AE8C35A"));
            byte[] output = new byte[16];

            engine.DecryptBlock(FromHex("019F9809DE1711858FAAC3A3BA20FBC3"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("D491DB16E7B1C39E86CB086B789F5419"), output);
        }

        [TestCase("00000000000000000000000000000000")]
        [TestCase("000102030405060708090A0B0C0D0E0F1011121314151617")]
        [TestCase("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F")]
        public void RoundTrip_RestoresPlaintext(string keyHex)
        {
            var engine = new TwofishEngine(FromHex(keyHex));
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
            Assert.AreEqual(16, new TwofishEngine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new TwofishEngine(null));
            Assert.Throws<System.ArgumentException>(() => new TwofishEngine(new byte[8]));
            Assert.Throws<System.ArgumentException>(() => new TwofishEngine(new byte[15]));
            Assert.Throws<System.ArgumentException>(() => new TwofishEngine(new byte[17]));
            Assert.Throws<System.ArgumentException>(() => new TwofishEngine(new byte[40]));
            Assert.DoesNotThrow(() => new TwofishEngine(new byte[16]));
            Assert.DoesNotThrow(() => new TwofishEngine(new byte[24]));
            Assert.DoesNotThrow(() => new TwofishEngine(new byte[32]));
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
