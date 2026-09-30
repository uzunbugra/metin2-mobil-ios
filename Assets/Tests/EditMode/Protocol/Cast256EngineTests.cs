using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class Cast256EngineTests
    {
        // KAT sources: RFC 2612 Appendix A (128/192/256-bit, PT = all-zero).
        [TestCase(
            "2342bb9efa38542c0af75647f29f615d",
            "00000000000000000000000000000000",
            "c842a08972b43d20836c91d1b7530f6b")]
        [TestCase(
            "2342bb9efa38542cbed0ac83940ac298bac77a7717942863",
            "00000000000000000000000000000000",
            "1b386c0210dcadcbdd0e41aa08a7a7e8")]
        [TestCase(
            "2342bb9efa38542cbed0ac83940ac2988d7c47ce264908461cc1b5137ae6b604",
            "00000000000000000000000000000000",
            "4f6a2038286897b9c9870136553317fa")]
        public void EncryptBlock_MatchesCast256KAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new Cast256Engine(FromHex(keyHex));
            byte[] keystream = new byte[16];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesKAT()
        {
            var engine = new Cast256Engine(FromHex("2342bb9efa38542cbed0ac83940ac2988d7c47ce264908461cc1b5137ae6b604"));
            byte[] output = new byte[16];

            engine.DecryptBlock(FromHex("4f6a2038286897b9c9870136553317fa"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("00000000000000000000000000000000"), output);
        }

        [TestCase("2342bb9efa38542c0af75647f29f615d")]
        [TestCase("2342bb9efa38542cbed0ac83940ac2988d7c47ce")]
        [TestCase("2342bb9efa38542cbed0ac83940ac298bac77a7717942863")]
        [TestCase("2342bb9efa38542cbed0ac83940ac2988d7c47ce264908461cc1b513")]
        [TestCase("2342bb9efa38542cbed0ac83940ac2988d7c47ce264908461cc1b5137ae6b604")]
        public void RoundTrip_RestoresPlaintext(string keyHex)
        {
            var engine = new Cast256Engine(FromHex(keyHex));
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
            Assert.AreEqual(16, new Cast256Engine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new Cast256Engine(null));
            Assert.Throws<System.ArgumentException>(() => new Cast256Engine(new byte[8]));
            Assert.Throws<System.ArgumentException>(() => new Cast256Engine(new byte[15]));
            Assert.Throws<System.ArgumentException>(() => new Cast256Engine(new byte[17]));
            Assert.Throws<System.ArgumentException>(() => new Cast256Engine(new byte[33]));
            Assert.DoesNotThrow(() => new Cast256Engine(new byte[16]));
            Assert.DoesNotThrow(() => new Cast256Engine(new byte[20]));
            Assert.DoesNotThrow(() => new Cast256Engine(new byte[24]));
            Assert.DoesNotThrow(() => new Cast256Engine(new byte[28]));
            Assert.DoesNotThrow(() => new Cast256Engine(new byte[32]));
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
