using System.Numerics;
using NUnit.Framework;
using Metin2.Protocol.Security;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class Dh2KeyAgreementTests
    {
        [Test]
        public void GroupConstants_MatchSourceBitLengths()
        {
            // RFC 5114 1024-bit MODP + 160-bit subgroup (cipher.cpp:307-324).
            // ToFixedBigEndian throws when the value does not fit: these asserts
            // fail if any quoted hex digit was miscopied.
            Assert.DoesNotThrow(() => DiffieHellmanGroup.ToFixedBigEndian(DiffieHellmanGroup.P, 128));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => DiffieHellmanGroup.ToFixedBigEndian(DiffieHellmanGroup.P, 127));
            Assert.DoesNotThrow(() => DiffieHellmanGroup.ToFixedBigEndian(DiffieHellmanGroup.Q, 20));
            Assert.IsTrue(DiffieHellmanGroup.G > BigInteger.One);
            Assert.IsTrue(DiffieHellmanGroup.G < DiffieHellmanGroup.P);
        }

        [Test]
        public void GroupConstants_SubgroupOrderVerifies()
        {
            // Mirrors the Prepare group validation (cipher.cpp:348-352): g^q == 1 (mod p).
            Assert.AreEqual(
                BigInteger.One,
                BigInteger.ModPow(DiffieHellmanGroup.G, DiffieHellmanGroup.Q, DiffieHellmanGroup.P));
        }

        [Test]
        public void Generate_ExportPublicData_Is256Bytes()
        {
            using (var agreement = Dh2KeyAgreement.Generate())
            {
                byte[] pub = agreement.ExportPublicData();
                Assert.AreEqual(DiffieHellmanGroup.KeyDataLength, pub.Length);
                Assert.AreEqual(256, pub.Length);
            }
        }

        [Test]
        public void Generate_TwoInstances_Differ()
        {
            using (var a = Dh2KeyAgreement.Generate())
            using (var b = Dh2KeyAgreement.Generate())
            {
                CollectionAssert.AreNotEqual(a.ExportPublicData(), b.ExportPublicData());
            }
        }

        [Test]
        public void TwoParties_DeriveIdenticalSharedSecret()
        {
            using (var alice = Dh2KeyAgreement.Generate())
            using (var bob = Dh2KeyAgreement.Generate())
            {
                byte[] alicePub = alice.ExportPublicData();
                byte[] bobPub = bob.ExportPublicData();

                Assert.IsTrue(alice.TryAgree(DiffieHellmanGroup.AgreedValueLength, bobPub, out byte[] aliceShared));
                Assert.IsTrue(bob.TryAgree(DiffieHellmanGroup.AgreedValueLength, alicePub, out byte[] bobShared));

                Assert.AreEqual(256, aliceShared.Length);
                CollectionAssert.AreEqual(aliceShared, bobShared);

                // Shared secret must not be degenerate.
                bool anyNonZero = false;
                foreach (byte v in aliceShared)
                {
                    if (v != 0)
                    {
                        anyNonZero = true;
                        break;
                    }
                }
                Assert.IsTrue(anyNonZero);
            }
        }

        [Test]
        public void TryAgree_WrongAgreedLength_Rejected()
        {
            using (var alice = Dh2KeyAgreement.Generate())
            using (var bob = Dh2KeyAgreement.Generate())
            {
                Assert.IsFalse(alice.TryAgree(128, bob.ExportPublicData(), out byte[] shared));
                Assert.IsNull(shared);
            }
        }

        [TestCase(255)]
        [TestCase(257)]
        [TestCase(0)]
        public void TryAgree_WrongDataLength_Rejected(int length)
        {
            using (var alice = Dh2KeyAgreement.Generate())
            {
                Assert.IsFalse(alice.TryAgree(DiffieHellmanGroup.AgreedValueLength, new byte[length], out byte[] shared));
                Assert.IsNull(shared);
            }
        }

        [Test]
        public void TryAgree_NullData_Rejected()
        {
            using (var alice = Dh2KeyAgreement.Generate())
            {
                Assert.IsFalse(alice.TryAgree(DiffieHellmanGroup.AgreedValueLength, null, out byte[] shared));
                Assert.IsNull(shared);
            }
        }

        [Test]
        public void TryAgree_CorruptedPeerKey_Rejected()
        {
            using (var alice = Dh2KeyAgreement.Generate())
            using (var bob = Dh2KeyAgreement.Generate())
            {
                byte[] corrupted = bob.ExportPublicData();
                // 0xFF top bytes push the static half above p: deterministic range reject.
                for (int i = 0; i < 8; i++)
                {
                    corrupted[i] = 0xFF;
                }

                Assert.IsFalse(alice.TryAgree(DiffieHellmanGroup.AgreedValueLength, corrupted, out byte[] shared));
                Assert.IsNull(shared);
            }
        }
    }
}
