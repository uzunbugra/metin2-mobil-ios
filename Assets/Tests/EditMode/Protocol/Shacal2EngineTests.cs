using NUnit.Framework;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class Shacal2EngineTests
    {
        // KAT sources (64-byte keys): CryptoPP TestVectors/shacal2.txt,
        // "Source: NESSIE submission" (SHACAL-2/ECB). Byte strings as published
        // (SHACAL-2 is big-endian throughout — no word-order conversion needed,
        // unlike RC5).
        [TestCase(
            "80000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
            "0000000000000000000000000000000000000000000000000000000000000000",
            "361AB6322FA9E7A7BB23818D839E01BDDAFDF47305426EDD297AEDB9F6202BAE")]
        [TestCase(
            "40000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
            "0000000000000000000000000000000000000000000000000000000000000000",
            "F3BAF53E5301E08813F8BE6F651BB19E9722151FF15063BA42A6FEF7CF3BF3D7")]
        [TestCase(
            "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
            "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
            "0598127BAF11706F77402000D730C54A0B84C868A98C6CA4D7F3C0FA06A78B7A")]
        public void EncryptBlock_MatchesNessieKAT(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new Shacal2Engine(FromHex(keyHex));
            byte[] keystream = new byte[32];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        // 16-byte (wire-size) keys: no published vectors found (all NESSIE vectors
        // use 512-bit keys). Expected values below were computed by an independent
        // Python reference port of the same CryptoPP shacal2.cpp source (explicit
        // shift-form rounds vs C#'s rotation-form port — different code paths, same
        // spec); the Python port itself was validated against the 3 NESSIE vectors
        // above before generating these. This proves the short-key zero-padding path
        // (GetUserKey, misc.h) plus guards against C#-specific coding errors.
        [TestCase(
            "00000000000000000000000000000000",
            "0000000000000000000000000000000000000000000000000000000000000000",
            "7CA51614425C3BA8CE54DD2FC2020AE7B6E574D198136D0FAE7E26CCBF0BE7A6")]
        [TestCase(
            "000102030405060708090A0B0C0D0E0F",
            "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F",
            "34DBDD8FF4018EAFF2A4E9D208EDCB24AEF0B238AD8E168893E9F915C6D8A9F4")]
        [TestCase(
            "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
            "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
            "9CEC13DBF8460A1922FC60A0AB78B9C98D57925A062755E6FA0293C5F43E9675")]
        public void EncryptBlock_ShortKey_MatchesPythonReference(string keyHex, string plainHex, string expectedHex)
        {
            var engine = new Shacal2Engine(FromHex(keyHex));
            byte[] keystream = new byte[32];

            engine.EncryptBlock(FromHex(plainHex), 0, keystream, 0);

            CollectionAssert.AreEqual(FromHex(expectedHex), keystream);
        }

        [Test]
        public void DecryptBlock_ReversesNessieKAT()
        {
            var engine = new Shacal2Engine(FromHex(
                "80000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
            byte[] output = new byte[32];

            engine.DecryptBlock(
                FromHex("361AB6322FA9E7A7BB23818D839E01BDDAFDF47305426EDD297AEDB9F6202BAE"), 0, output, 0);

            CollectionAssert.AreEqual(new byte[32], output);
        }

        [Test]
        public void DecryptBlock_ReversesShortKeyCiphertext()
        {
            var engine = new Shacal2Engine(FromHex("000102030405060708090A0B0C0D0E0F"));
            byte[] output = new byte[32];

            engine.DecryptBlock(
                FromHex("34DBDD8FF4018EAFF2A4E9D208EDCB24AEF0B238AD8E168893E9F915C6D8A9F4"), 0, output, 0);

            CollectionAssert.AreEqual(
                FromHex("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F"), output);
        }

        [TestCase("00000000000000000000000000000000")]
        [TestCase("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F")]
        public void RoundTrip_RestoresPlaintext(string keyHex)
        {
            var engine = new Shacal2Engine(FromHex(keyHex));
            byte[] block = FromHex("FEDCBA98765432100123456789ABCDEF0123456789ABCDEFFEDCBA9876543210");
            byte[] encrypted = new byte[32];
            byte[] decrypted = new byte[32];

            engine.EncryptBlock(block, 0, encrypted, 0);
            Assert.AreNotEqual(block, encrypted);

            engine.DecryptBlock(encrypted, 0, decrypted, 0);
            CollectionAssert.AreEqual(block, decrypted);
        }

        [Test]
        public void BlockSize_IsThirtyTwo()
        {
            Assert.AreEqual(32, new Shacal2Engine(new byte[16]).BlockSize);
        }

        [Test]
        public void InvalidKey_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new Shacal2Engine(null));
            Assert.Throws<System.ArgumentException>(() => new Shacal2Engine(new byte[15]));
            Assert.Throws<System.ArgumentException>(() => new Shacal2Engine(new byte[65]));
            Assert.DoesNotThrow(() => new Shacal2Engine(new byte[16]));
            Assert.DoesNotThrow(() => new Shacal2Engine(new byte[24]));
            Assert.DoesNotThrow(() => new Shacal2Engine(new byte[64]));
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
