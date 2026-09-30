using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class BlowfishEngineTests
    {
        // KAT sources: Schneier/Eric Young official vectors
        // (https://www.schneier.com/.../vectors-2.txt, also reproduced as
        // draft-schneier-blowfish-00 §4; "012345 loads as data[0]=0x01" —
        // plain byte strings, big-endian words, no word-order conversion).
        [TestCase(
            "0000000000000000",
            "0000000000000000",
            "4EF997456198DD78")]
        [TestCase(
            "FFFFFFFFFFFFFFFF",
            "FFFFFFFFFFFFFFFF",
            "51866FD5B85ECB8A")]
        [TestCase(
            "3000000000000000",
            "1000000000000001",
            "7D856F9A613063F2")]
        [TestCase(
            "1111111111111111",
            "1111111111111111",
            "2466DD878B963C9D")]
        [TestCase(
            "FEDCBA9876543210",
            "0123456789ABCDEF",
            "0ACEAB0FC6A0A28D")]
        [TestCase(
            "7CA110454A1A6E57",
            "01A1D6D039776742",
            "59C68245EB05282B")]
        public void EncryptBlock_MatchesOfficialEcbKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new BlowfishEngine(FromHex(keyHex));
            byte[] keystream = new byte[8];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        // set_key vectors (same source): plaintext FEDCBA9876543210, key lengths
        // 1..24 bytes. These prove the variable-length key path — including k[16],
        // the wire key size (F0E1...0F -> 93142887EE3BE15C).
        [TestCase("F0E1D2C3", "BE1E639408640F05")]
        [TestCase("F0E1D2C3B4A59687", "E87A244E2CC85E82")]
        [TestCase("F0E1D2C3B4A5968778695A4B3C2D1E0F", "93142887EE3BE15C")]
        [TestCase("F0E1D2C3B4A5968778695A4B3C2D1E0F0011223344556677", "05044B62FA52D080")]
        public void EncryptBlock_MatchesOfficialSetKeyKAT(string keyHex, string expectedHex)
        {
            var engine = new BlowfishEngine(FromHex(keyHex));
            byte[] keystream = new byte[8];

            engine.EncryptBlock(FromHex("FEDCBA9876543210"), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesEcbKAT()
        {
            var engine = new BlowfishEngine(FromHex("FEDCBA9876543210"));
            byte[] output = new byte[8];

            engine.DecryptBlock(FromHex("0ACEAB0FC6A0A28D"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("0123456789ABCDEF"), output);
        }

        [Test]
        public void DecryptBlock_ReversesSetKey16KAT()
        {
            var engine = new BlowfishEngine(FromHex("F0E1D2C3B4A5968778695A4B3C2D1E0F"));
            byte[] output = new byte[8];

            engine.DecryptBlock(FromHex("93142887EE3BE15C"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("FEDCBA9876543210"), output);
        }

        [Test]
        public void RoundTrip_WireSizeKey_RestoresPlaintext()
        {
            var engine = new BlowfishEngine(FromHex("3A98BF20CB791CBCB313456DDE2C5F30"));
            byte[] block = FromHex("FEDCBA9876543210");
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
            Assert.AreEqual(8, new BlowfishEngine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new BlowfishEngine(null));
            Assert.Throws<System.ArgumentException>(() => new BlowfishEngine(new byte[3]));
            Assert.Throws<System.ArgumentException>(() => new BlowfishEngine(new byte[57]));
            Assert.DoesNotThrow(() => new BlowfishEngine(new byte[4]));
            Assert.DoesNotThrow(() => new BlowfishEngine(new byte[8]));
            Assert.DoesNotThrow(() => new BlowfishEngine(new byte[16]));
            Assert.DoesNotThrow(() => new BlowfishEngine(new byte[56]));
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
