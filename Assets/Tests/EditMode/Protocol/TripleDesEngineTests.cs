using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class TripleDesEngineTests
    {
        // DES-EDE2 has no single official 2-key ECB vector file, so the core is
        // proven through degeneracy: EDE2(K‖K) == DES(K). Vectors below are
        // NIST SP 800-17 (via PyCryptodome test_DES.py): Appendix A sample round
        // outputs + Table B.1 variable-plaintext KAT.
        [TestCase(
            "10316E028C8F3B4A10316E028C8F3B4A",
            "0000000000000000",
            "82DCBAFBDEAB6602")]
        [TestCase(
            "01010101010101010101010101010101",
            "8000000000000000",
            "95F8A5E5DD31D900")]
        public void EncryptBlock_DegenerateKey_MatchesNistDesKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new TripleDesEngine(FromHex(keyHex));
            byte[] keystream = new byte[8];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void EncryptBlock_RivestDestestRecurrence_MatchesX16()
        {
            // Rivest Destest (people.csail.mit.edu/rivest/Destest.txt, via
            // PyCryptodome test_DES.py): X0 = 9474B8E8C73BCA7D, then alternate
            // E(Xi,Xi) / D(Xi,Xi) with key = Xi. X16 must be 1B1A2DDB4C642438.
            // At EDE2 level the key is Xi‖Xi (degenerate single DES each step).
            byte[] x = FromHex("9474B8E8C73BCA7D");
            for (int i = 0; i < 16; i++)
            {
                byte[] key = new byte[16];
                System.Buffer.BlockCopy(x, 0, key, 0, 8);
                System.Buffer.BlockCopy(x, 0, key, 8, 8);
                var engine = new TripleDesEngine(key);
                byte[] next = new byte[8];
                if (i % 2 == 0)
                {
                    engine.EncryptBlock(x, 0, next, 0);
                }
                else
                {
                    engine.DecryptBlock(x, 0, next, 0);
                }

                x = next;
            }

            CollectionAssert.AreEqual(FromHex("1B1A2DDB4C642438"), x);
        }

        // Genuine 2-key (K1 != K2) vectors: PyCryptodome's DES3 self-test vector
        // plus two outputs of an independent Python DES reference port (textbook
        // FIPS tables; validated against the NIST vectors above before generating).
        [TestCase(
            "9B397EBF81B1181E282F4BB8ADBADC6B",
            "21E81B7ADE88A259",
            "5C577D4D9B20C0F8")]
        [TestCase(
            "0123456789ABCDEF23456789ABCDEF01",
            "0123456789ABCDEF",
            "A6BB373E196B375E")]
        [TestCase(
            "FEDCBA98765432100011223344556677",
            "0011223344556677",
            "853261EA147D5FF2")]
        public void EncryptBlock_TwoKeys_MatchesIndependentVectors(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new TripleDesEngine(FromHex(keyHex));
            byte[] keystream = new byte[8];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesTwoKeyKAT()
        {
            var engine = new TripleDesEngine(FromHex("9B397EBF81B1181E282F4BB8ADBADC6B"));
            byte[] output = new byte[8];

            engine.DecryptBlock(FromHex("5C577D4D9B20C0F8"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("21E81B7ADE88A259"), output);
        }

        [Test]
        public void RoundTrip_WireSizeKey_RestoresPlaintext()
        {
            var engine = new TripleDesEngine(FromHex("3A98BF20CB791CBCB313456DDE2C5F30"));
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
            Assert.AreEqual(8, new TripleDesEngine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new TripleDesEngine(null));
            Assert.Throws<System.ArgumentException>(() => new TripleDesEngine(new byte[8]));
            Assert.Throws<System.ArgumentException>(() => new TripleDesEngine(new byte[24]));
            Assert.DoesNotThrow(() => new TripleDesEngine(new byte[16]));
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
