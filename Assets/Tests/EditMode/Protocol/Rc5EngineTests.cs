using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class Rc5EngineTests
    {
        // KAT sources: Rivest "The RC5 Encryption Algorithm" (1994, rev. 1997),
        // RC5-32/12/16 chaining vectors (each ciphertext feeds the next plaintext).
        // Algorithm description cross-checked against RFC 2040 §5–6
        // (P32/Q32, little-endian packing, key schedule, round function).
        // NOTE: published vectors are r=12; the wire default is r=16
        // (CryptoPP VariableRounds<16>), so tests construct with rounds: 12.
        // BYTE ORDER: Rivest prints words (e.g. A=EEDBA521 B=6D8F4B15); the
        // wire serializes each word LITTLE-endian per RFC 2040 §6.1/§6.3
        // (same convention as RC6 — see SPRINT_03 lessons). Expected strings
        // below are the LE byte serializations, e.g. EEDBA521 -> 21A5DBEE.
        [TestCase(
            "00000000000000000000000000000000",
            "0000000000000000",
            "21A5DBEE154B8F6D")]
        [TestCase(
            "915F4619BE41B2516355A50110A9CE91",
            "21A5DBEE154B8F6D",
            "F7C013AC5B2B8952")]
        [TestCase(
            "783348E75AEB0F2FD7B169BB8DC16787",
            "F7C013AC5B2B8952",
            "2F42B3B70369FC92")]
        [TestCase(
            "DC49DB1375A5584F6485B413B5F12BAF",
            "2F42B3B70369FC92",
            "65C178B284D197CC")]
        [TestCase(
            "5269F149D41BA0152497574D7F153125",
            "65C178B284D197CC",
            "EB44E415DA319824")]
        public void EncryptBlock_R12_MatchesRivestKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new Rc5Engine(FromHex(keyHex), 12);
            byte[] keystream = new byte[8];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_R12_ReversesKAT()
        {
            var engine = new Rc5Engine(FromHex("915F4619BE41B2516355A50110A9CE91"), 12);
            byte[] output = new byte[8];

            engine.DecryptBlock(FromHex("F7C013AC5B2B8952"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("21A5DBEE154B8F6D"), output);
        }

        [Test]
        public void RoundTrip_DefaultRounds16_RestoresPlaintext()
        {
            // Default constructor must be CryptoPP-compatible r=16: round-trips
            // and differs from r=12 output (parametrization is live, not ignored).
            var engine16 = new Rc5Engine(FromHex("3A98BF20CB791CBCB313456DDE2C5F30"));
            Assert.AreEqual(16, engine16.Rounds);
            byte[] block = FromHex("FEDCBA9876543210");
            byte[] encrypted16 = new byte[8];
            byte[] decrypted = new byte[8];

            engine16.EncryptBlock(block, 0, encrypted16, 0);
            Assert.AreNotEqual(block, encrypted16);

            engine16.DecryptBlock(encrypted16, 0, decrypted, 0);
            CollectionAssert.AreEqual(block, decrypted);

            var engine12 = new Rc5Engine(FromHex("3A98BF20CB791CBCB313456DDE2C5F30"), 12);
            byte[] encrypted12 = new byte[8];
            engine12.EncryptBlock(block, 0, encrypted12, 0);
            Assert.AreNotEqual(encrypted12, encrypted16);
        }

        [Test]
        public void BlockSize_IsEight_DefaultRoundsIsSixteen()
        {
            Assert.AreEqual(8, new Rc5Engine(new byte[16]).BlockSize);
            Assert.AreEqual(16, new Rc5Engine(new byte[16]).Rounds);
            Assert.AreEqual(12, new Rc5Engine(new byte[16], 12).Rounds);
        }

        [Test]
        public void InvalidKeyOrRounds_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new Rc5Engine(null));
            Assert.Throws<System.ArgumentException>(() => new Rc5Engine(new byte[8]));
            Assert.Throws<System.ArgumentException>(() => new Rc5Engine(new byte[24]));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Rc5Engine(new byte[16], -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Rc5Engine(new byte[16], 256));
            Assert.DoesNotThrow(() => new Rc5Engine(new byte[16]));
            Assert.DoesNotThrow(() => new Rc5Engine(new byte[16], 0));
            Assert.DoesNotThrow(() => new Rc5Engine(new byte[16], 12));
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
