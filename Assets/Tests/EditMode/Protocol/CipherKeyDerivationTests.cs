using NUnit.Framework;
using Metin2.Protocol.Security;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class CipherKeyDerivationTests
    {
        private static byte[] SequentialShared()
        {
            byte[] shared = new byte[256];
            for (int i = 0; i < shared.Length; i++)
            {
                shared[i] = (byte)i;
            }
            return shared;
        }

        [Test]
        public void Derive_SequentialShared_SelectsTwofishAndRC6()
        {
            // Hand-traced from SetUp (cipher.cpp:186-228):
            // shared[0]=0 -> hint_0 = shared[0] = 0 -> Pick(0) = Twofish (blk 16)
            // shared[1]=1 -> hint_1 = shared[1] = 1 -> Pick(1) = RC6 (blk 16)
            Assert.IsTrue(CipherKeyDerivation.TryDerive(SequentialShared(), out CipherKeyMaterial material));

            Assert.AreEqual(CipherSuite.Twofish, material.Direction0.Suite);
            Assert.AreEqual(CipherSuite.RC6, material.Direction1.Suite);

            // key_0 = shared[0..16); offset = min(16, 256-16) = 16; key_1 = shared[16..32)
            Assert.AreEqual(16, material.Direction0.Key.Length);
            Assert.AreEqual(16, material.Direction1.Key.Length);
            Assert.AreEqual(0, material.Direction0.Key[0]);
            Assert.AreEqual(15, material.Direction0.Key[15]);
            Assert.AreEqual(16, material.Direction1.Key[0]);
            Assert.AreEqual(31, material.Direction1.Key[15]);

            // iv_0 = last 16 bytes; iv_1 = 16 bytes before iv_0
            Assert.AreEqual(16, material.Direction0.IV.Length);
            Assert.AreEqual(16, material.Direction1.IV.Length);
            Assert.AreEqual(240, material.Direction0.IV[0]);
            Assert.AreEqual(255, material.Direction0.IV[15]);
            Assert.AreEqual(224, material.Direction1.IV[0]);
            Assert.AreEqual(239, material.Direction1.IV[15]);
        }

        [Test]
        public void Derive_MixedBlockSizes_SlicesKeysAndIVs()
        {
            // Hand-traced: shared[0]=5 -> hint_0 = shared[5] = 27 -> 27%14=13 SHACAL-2 (iv 32)
            // shared[1]=7 -> hint_1 = shared[7] = 24 -> 24%14=10 RC5 (iv 8)
            byte[] shared = new byte[256];
            for (int i = 0; i < shared.Length; i++)
            {
                shared[i] = 0xAA;
            }
            shared[0] = 5;
            shared[5] = 27;
            shared[1] = 7;
            shared[7] = 24;

            Assert.IsTrue(CipherKeyDerivation.TryDerive(shared, out CipherKeyMaterial material));

            Assert.AreEqual(CipherSuite.SHACAL2, material.Direction0.Suite);
            Assert.AreEqual(CipherSuite.RC5, material.Direction1.Suite);

            // Keys are always 16 bytes: key_0 = shared[0..16), key_1 = shared[16..32)
            Assert.AreEqual(5, material.Direction0.Key[0]);
            Assert.AreEqual(7, material.Direction0.Key[1]);
            Assert.AreEqual(27, material.Direction0.Key[5]);
            Assert.AreEqual(0xAA, material.Direction1.Key[0]);

            // iv_0 = shared[224..256) (32B); iv_1 = shared[216..224) (8B)
            Assert.AreEqual(32, material.Direction0.IV.Length);
            Assert.AreEqual(8, material.Direction1.IV.Length);
            Assert.AreEqual(0xAA, material.Direction0.IV[0]);
            Assert.AreEqual(0xAA, material.Direction0.IV[31]);
            Assert.AreEqual(0xAA, material.Direction1.IV[0]);
            Assert.AreEqual(0xAA, material.Direction1.IV[7]);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Derive_TooShortInput_FailsClosed(int length)
        {
            // Mirrors: shared.size() < 2 -> false (cipher.cpp:186-188).
            Assert.IsFalse(CipherKeyDerivation.TryDerive(new byte[length], out _));
        }

        [Test]
        public void Derive_NullInput_FailsClosed()
        {
            Assert.IsFalse(CipherKeyDerivation.TryDerive(null, out _));
        }

        [Test]
        public void Derive_OversizedIVForShortInput_FailsClosed()
        {
            // 10 zero bytes: hint_0 = shared[0] = 0 -> Twofish needs 16B key/IV > 10.
            Assert.IsFalse(CipherKeyDerivation.TryDerive(new byte[10], out _));
        }
    }
}
