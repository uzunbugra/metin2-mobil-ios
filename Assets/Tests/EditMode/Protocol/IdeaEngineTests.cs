using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class IdeaEngineTests
    {
        // KAT sources: Handbook of Applied Cryptography Table 7.12
        // (K = 0001..0008, P = 0000 0001 0002 0003 -> C = 11FB ED2B 0198 6DE5)
        // and the ETH reference-implementation vector set (source-code.biz).
        [TestCase(
            "00010002000300040005000600070008",
            "0000000100020003",
            "11FBED2B01986DE5")]
        [TestCase(
            "729A27ED8F5C3E8BAF16560D14C90B43",
            "D53FABBF94FF8B5F",
            "1D0CB2AF1654820A")]
        [TestCase(
            "729A27ED8F5C3E8BAF16560D14C90B43",
            "848F836780938169",
            "D7E0468226D0FC56")]
        [TestCase(
            "000027ED8F5C3E8BAF16560D14C90B43",
            "D53FABBF94FF8B5F",
            "1320F99BFE052804")]
        public void EncryptBlock_MatchesPublishedKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new IdeaEngine(FromHex(keyHex));
            byte[] keystream = new byte[8];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesKAT()
        {
            var engine = new IdeaEngine(FromHex("00010002000300040005000600070008"));
            byte[] output = new byte[8];

            engine.DecryptBlock(FromHex("11FBED2B01986DE5"), 0, output, 0);

            CollectionAssert.AreEqual(FromHex("0000000100020003"), output);
        }

        [Test]
        public void RoundTrip_RandomishKey_RestoresPlaintext()
        {
            var engine = new IdeaEngine(FromHex("3A984E2000195DB32EE501C8C47CEA60"));
            byte[] block = FromHex("0102030405060708");
            byte[] encrypted = new byte[8];
            byte[] decrypted = new byte[8];

            engine.EncryptBlock(block, 0, encrypted, 0);
            Assert.AreNotEqual(block, encrypted);

            engine.DecryptBlock(encrypted, 0, decrypted, 0);
            CollectionAssert.AreEqual(block, decrypted);
        }

        [Test]
        public void KeySchedule_MatchesHACRoundKeys()
        {
            // HAC Table 7.12 round subkeys for K = (0001..0008), cumulative
            // 25-bit left rotation per 8-subkey group. (An earlier draft of this
            // test mislabeled rot2 words as E18..23; they are E16..23 — the
            // engine and HAC row 4 ("0018 001c ...") were right all along.)
            ushort[] expected = new ushort[]
            {
                0x0001, 0x0002, 0x0003, 0x0004, 0x0005, 0x0006,
                0x0007, 0x0008, 0x0400, 0x0600, 0x0800, 0x0A00,
                0x0C00, 0x0E00, 0x1000, 0x0200, 0x0010, 0x0014,
                0x0018, 0x001C, 0x0020, 0x0004, 0x0008, 0x000C,
                0x2800, 0x3000, 0x3800, 0x4000, 0x0800, 0x1000,
                0x1800, 0x2000, 0x0070, 0x0080, 0x0010, 0x0020,
                0x0030, 0x0040, 0x0050, 0x0060, 0x0000, 0x2000,
                0x4000, 0x6000, 0x8000, 0xA000, 0xC000, 0xE001,
                0x0080, 0x00C0, 0x0100, 0x0140
            };

            var engine = new IdeaEngine(FromHex("00010002000300040005000600070008"));
            CollectionAssert.AreEqual(expected, engine.GetEncryptionKeysForTest());
        }

        [Test]
        public void BlockSize_IsEight()
        {
            Assert.AreEqual(8, new IdeaEngine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new IdeaEngine(null));
            Assert.Throws<System.ArgumentException>(() => new IdeaEngine(new byte[8]));
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
