using NUnit.Framework;
using Metin2.Protocol.Security;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class CipherSuiteTests
    {
        [TestCase(0, CipherSuite.Twofish)]   // kDefault -> default algorithm (cipher.cpp:293-296)
        [TestCase(1, CipherSuite.RC6)]
        [TestCase(2, CipherSuite.MARS)]
        [TestCase(3, CipherSuite.Twofish)]
        [TestCase(4, CipherSuite.Serpent)]
        [TestCase(5, CipherSuite.CAST256)]
        [TestCase(6, CipherSuite.IDEA)]
        [TestCase(7, CipherSuite.TripleDES)]
        [TestCase(8, CipherSuite.Camellia)]
        [TestCase(9, CipherSuite.SEED)]
        [TestCase(10, CipherSuite.RC5)]
        [TestCase(11, CipherSuite.Blowfish)]
        [TestCase(12, CipherSuite.TEA)]
        [TestCase(13, CipherSuite.SHACAL2)]
        [TestCase(14, CipherSuite.Twofish)]  // wraps: 14 % 14 == 0
        [TestCase(27, CipherSuite.SHACAL2)]  // 27 % 14 == 13
        [TestCase(255, CipherSuite.Twofish)] // 255 % 14 == 3
        public void Pick_MapsHintToSourceVerifiedSuite(int hint, CipherSuite expected)
        {
            Assert.AreEqual(expected, CipherSuiteTable.Pick(hint));
        }

        [Test]
        public void SuiteCount_MatchesKMaxAlgorithms()
        {
            Assert.AreEqual(14, CipherSuiteTable.SuiteCount);
        }

        [TestCase(CipherSuite.RC6, 16)]
        [TestCase(CipherSuite.MARS, 16)]
        [TestCase(CipherSuite.Twofish, 16)]
        [TestCase(CipherSuite.Serpent, 16)]
        [TestCase(CipherSuite.CAST256, 16)]
        [TestCase(CipherSuite.IDEA, 8)]
        [TestCase(CipherSuite.TripleDES, 8)]
        [TestCase(CipherSuite.Camellia, 16)]
        [TestCase(CipherSuite.SEED, 16)]
        [TestCase(CipherSuite.RC5, 8)]
        [TestCase(CipherSuite.Blowfish, 8)]
        [TestCase(CipherSuite.TEA, 8)]
        [TestCase(CipherSuite.SHACAL2, 32)]
        public void GetBlockSize_MatchesVendoredCryptoPPHeaders(CipherSuite suite, int expected)
        {
            Assert.AreEqual(expected, CipherSuiteTable.GetBlockSize(suite));
        }

        [Test]
        public void GetDefaultKeyLength_Is16ForEverySuite()
        {
            // VERIFIED: all suites are FixedKeyLength<16> / VariableKeyLength<16,...>.
            foreach (CipherSuite suite in System.Enum.GetValues(typeof(CipherSuite)))
            {
                Assert.AreEqual(16, CipherSuiteTable.GetDefaultKeyLength(suite), suite.ToString());
            }
        }
    }
}
