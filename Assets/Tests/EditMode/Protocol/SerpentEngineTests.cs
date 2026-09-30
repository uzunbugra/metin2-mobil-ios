using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class SerpentEngineTests
    {
        // KAT sources (128-bit, key = 0): Botan src/tests/data/block/serpent.vec
        // block pairs, machine-extracted (AES submission vectors). Byte strings
        // as published (Serpent is little-endian throughout).
        [TestCase(
            "D29D576FCEA3A3A7ED9099F29273D78E",
            "B2288B968AE8B08648D1CE9606FD992D")]
        [TestCase(
            "2D62A890CEA3A3A7ED9099F29273D78E",
            "717EB02EB81A2E939D54ACA91087112D")]
        [TestCase(
            "D29D576F315C5C58ED9099F29273D78E",
            "0D809C5EE82F477EBA7B956DBB23463B")]
        [TestCase(
            "2D62A890315C5C58ED9099F29273D78E",
            "0F0190D616F5294112FFB7884E8B37F9")]
        [TestCase(
            "D29D576FCEA3A3A7126F660D9273D78E",
            "41BA1B505386B7428B88338188F7E718")]
        public void EncryptBlock_ZeroKey_MatchesBotanKAT(string plainHex, string expectedHex)
        {
            var engine = new SerpentEngine(new byte[16]);
            byte[] keystream = new byte[16];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        // LibTomCrypt serpent_test() vectors (serpent.c): single-bit keys at
        // 16/24/32-byte sizes, plaintext zero. These prove the key schedule for
        // non-zero keys at every supported length.
        [TestCase(
            "80000000000000000000000000000000",
            "264E5481EFF42A4606ABDA06C0BFDA3D")]
        [TestCase(
            "40000000000000000000000000000000",
            "4A231B3BC727993407AC6EC8350E8524")]
        [TestCase(
            "20000000000000000000000000000000",
            "E03269F9E9FD853C7D8156DF14B98D56")]
        public void EncryptBlock_SingleBitKey16_MatchesLibTomCryptKAT(string keyHex, string expectedHex)
        {
            var engine = new SerpentEngine(FromHex(keyHex));
            byte[] keystream = new byte[16];

            engine.EncryptBlock(new byte[16], 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [TestCase(
            "D29D576FCEABA3A7ED9899F2927BD78E",
            "130E353E1037C22405E8FAEFB2C3C3E9",
            "000000000000000000000000000000000000000000000000")]
        [TestCase(
            "00000000000000000000000000000000",
            "9E274EAD9B737BB21EFCFCA548602689",
            "800000000000000000000000000000000000000000000000")]
        [TestCase(
            "D095576FCEA3E3A7ED98D9F29073D78E",
            "B90EE5862DE69168F2BDD5125B45472B",
            "0000000000000000000000000000000000000000000000000000000000000000")]
        [TestCase(
            "00000000000000000000000000000000",
            "A223AA1288463C0E2BE38EBD825616C0",
            "8000000000000000000000000000000000000000000000000000000000000000")]
        public void EncryptBlock_LongKeys_MatchesPublishedKAT(string plainHex, string expectedHex, string keyHex)
        {
            var engine = new SerpentEngine(FromHex(keyHex));
            byte[] keystream = new byte[16];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesKAT()
        {
            var engine = new SerpentEngine(new byte[16]);
            byte[] output = new byte[16];

            engine.DecryptBlock(FromHex("717EB02EB81A2E939D54ACA91087112D"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("2D62A890CEA3A3A7ED9099F29273D78E"), output);
        }

        [TestCase("00000000000000000000000000000000")]
        [TestCase("000102030405060708090A0B0C0D0E0F1011121314151617")]
        [TestCase("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F")]
        public void RoundTrip_RestoresPlaintext(string keyHex)
        {
            var engine = new SerpentEngine(FromHex(keyHex));
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
            Assert.AreEqual(16, new SerpentEngine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new SerpentEngine(null));
            Assert.Throws<System.ArgumentException>(() => new SerpentEngine(new byte[8]));
            Assert.Throws<System.ArgumentException>(() => new SerpentEngine(new byte[15]));
            Assert.Throws<System.ArgumentException>(() => new SerpentEngine(new byte[17]));
            Assert.Throws<System.ArgumentException>(() => new SerpentEngine(new byte[40]));
            Assert.DoesNotThrow(() => new SerpentEngine(new byte[16]));
            Assert.DoesNotThrow(() => new SerpentEngine(new byte[24]));
            Assert.DoesNotThrow(() => new SerpentEngine(new byte[32]));
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
