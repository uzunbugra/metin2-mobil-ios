using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class MarsEngineTests
    {
        // KAT sources: CryptoPP TestVectors/mars.txt (AES submission vectors).
        // All 128-bit (wire size) below except the last (192-bit, schedule path).
        [TestCase(
            "80000000000000000000000000000000",
            "00000000000000000000000000000000",
            "B3E2AD5608AC1B6733A7CB4FDF8F9952")]
        [TestCase(
            "00000000000000000000000000000000",
            "00000000000000000000000000000000",
            "DCC07B8DFB0738D6E30A22DFCF27E886")]
        [TestCase(
            "00000000000000000000000000000000",
            "DCC07B8DFB0738D6E30A22DFCF27E886",
            "33CAFFBDDC7F1DDA0F9C15FA2F30E2FF")]
        [TestCase(
            "CB14A1776ABBC1CDAFE7243DEF2CEA02",
            "F94512A9B42D034EC4792204D708A69B",
            "225DA2CB64B73F79069F21A5E3CB8522")]
        [TestCase(
            "86EDF4DA31824CABEF6A4637C40B0BAB",
            "4DF955AD5B398D66408D620A2B27E1A9",
            "A4B737340AE6D2CAFD930BA97D86129F")]
        [TestCase(
            "000000000000000000000000000000000000000000000000",
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            "97778747D60E425C2B4202599DB856FB")]
        public void EncryptBlock_MatchesMarsKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new MarsEngine(FromHex(keyHex));
            byte[] keystream = new byte[16];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesKAT()
        {
            var engine = new MarsEngine(FromHex("CB14A1776ABBC1CDAFE7243DEF2CEA02"));
            byte[] output = new byte[16];

            engine.DecryptBlock(FromHex("225DA2CB64B73F79069F21A5E3CB8522"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("F94512A9B42D034EC4792204D708A69B"), output);
        }

        [TestCase("00000000000000000000000000000000")]
        [TestCase("000102030405060708090A0B0C0D0E0F1011121314151617")]
        [TestCase("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F202122232425262728292A2B2C2D2E2F3031323334353637")]
        public void RoundTrip_RestoresPlaintext(string keyHex)
        {
            var engine = new MarsEngine(FromHex(keyHex));
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
            Assert.AreEqual(16, new MarsEngine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new MarsEngine(null));
            Assert.Throws<System.ArgumentException>(() => new MarsEngine(new byte[8]));
            Assert.Throws<System.ArgumentException>(() => new MarsEngine(new byte[15]));
            Assert.Throws<System.ArgumentException>(() => new MarsEngine(new byte[20]));
            Assert.Throws<System.ArgumentException>(() => new MarsEngine(new byte[57]));
            Assert.DoesNotThrow(() => new MarsEngine(new byte[16]));
            Assert.DoesNotThrow(() => new MarsEngine(new byte[24]));
            Assert.DoesNotThrow(() => new MarsEngine(new byte[56]));
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
