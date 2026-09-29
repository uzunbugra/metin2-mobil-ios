using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class Rc6EngineTests
    {
        // KAT sources: RC6 paper appendix (vectors 1-2, 16-byte keys),
        // IETF draft-krovetz-rc6-rc5-vectors (16/24/32-byte keys, incl. chaining vector).
        // RC6 packs words little-endian.
        [TestCase(
            "00000000000000000000000000000000",
            "00000000000000000000000000000000",
            "8FC3A53656B1F778C129DF4E9848A41E")]
        [TestCase(
            "0123456789ABCDEF0112233445566778",
            "02132435465768798A9BACBDCEDFE0F1",
            "524E192F4715C6231F51F6367EA43F18")]
        [TestCase(
            "000000000000000000000000000000000000000000000000",
            "00000000000000000000000000000000",
            "6CD61BCB190B30384E8A3F168690AE82")]
        [TestCase(
            "0123456789ABCDEF0112233445566778899AABBCCDDEEFF0",
            "02132435465768798A9BACBDCEDFE0F1",
            "688329D019E505041E52E92AF95291D4")]
        [TestCase(
            "000102030405060708090A0B0C0D0E0F",
            "000102030405060708090A0B0C0D0E0F",
            "3A96F9C7F6755CFE46F00E3DCD5D2A3C")]
        public void EncryptBlock_MatchesPublishedKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new Rc6Engine(FromHex(keyHex));
            byte[] keystream = new byte[16];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesKAT()
        {
            var engine = new Rc6Engine(FromHex("0123456789ABCDEF0112233445566778"));
            byte[] cipher = FromHex("524E192F4715C6231F51F6367EA43F18");
            byte[] output = new byte[16];

            engine.DecryptBlock(cipher, 0, output, 0);

            CollectionAssert.AreEqual(FromHex("02132435465768798A9BACBDCEDFE0F1"), output);
        }

        [Test]
        public void RoundTrip_NonTrivialKey_RestoresPlaintext()
        {
            var engine = new Rc6Engine(FromHex("3A98BF20CB791CBCB313456DDE2C5F30"));
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
            Assert.AreEqual(16, new Rc6Engine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new Rc6Engine(null));
            Assert.Throws<System.ArgumentException>(() => new Rc6Engine(new byte[15]));
            Assert.Throws<System.ArgumentException>(() => new Rc6Engine(new byte[20]));
            Assert.DoesNotThrow(() => new Rc6Engine(new byte[16]));
            Assert.DoesNotThrow(() => new Rc6Engine(new byte[24]));
            Assert.DoesNotThrow(() => new Rc6Engine(new byte[32]));
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
