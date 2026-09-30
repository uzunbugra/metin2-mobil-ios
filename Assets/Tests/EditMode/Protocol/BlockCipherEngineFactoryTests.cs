using NUnit.Framework;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Security;
using Metin2.Protocol.Security.Engines;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class BlockCipherEngineFactoryTests
    {
        [Test]
        public void IsSupported_TEA_RC6_IDEA_RC5_SHACAL2_Blowfish()
        {
            Assert.IsTrue(BlockCipherEngineFactory.IsSupported(CipherSuite.TEA));
            Assert.IsTrue(BlockCipherEngineFactory.IsSupported(CipherSuite.RC6));
            Assert.IsTrue(BlockCipherEngineFactory.IsSupported(CipherSuite.IDEA));
            Assert.IsTrue(BlockCipherEngineFactory.IsSupported(CipherSuite.RC5));
            Assert.IsTrue(BlockCipherEngineFactory.IsSupported(CipherSuite.SHACAL2));
            Assert.IsTrue(BlockCipherEngineFactory.IsSupported(CipherSuite.Blowfish));
            Assert.IsFalse(BlockCipherEngineFactory.IsSupported(CipherSuite.Twofish));
            Assert.IsFalse(BlockCipherEngineFactory.IsSupported(CipherSuite.MARS));
            Assert.IsFalse(BlockCipherEngineFactory.IsSupported(CipherSuite.Serpent));
        }

        [Test]
        public void Create_Blowfish_ReturnsWorkingEngine()
        {
            IBlockCipherEngine engine = BlockCipherEngineFactory.Create(CipherSuite.Blowfish, new byte[16]);
            Assert.AreEqual(8, engine.BlockSize);
        }

        [Test]
        public void Create_SHACAL2_ReturnsWorkingEngine()
        {
            IBlockCipherEngine engine = BlockCipherEngineFactory.Create(CipherSuite.SHACAL2, new byte[16]);
            Assert.AreEqual(32, engine.BlockSize);
        }

        [Test]
        public void Create_RC5_ReturnsWorkingEngine()
        {
            IBlockCipherEngine engine = BlockCipherEngineFactory.Create(CipherSuite.RC5, new byte[16]);
            Assert.AreEqual(8, engine.BlockSize);
        }

        [Test]
        public void Create_TEA_ReturnsWorkingEngine()
        {
            IBlockCipherEngine engine = BlockCipherEngineFactory.Create(CipherSuite.TEA, new byte[16]);
            Assert.AreEqual(8, engine.BlockSize);
        }

        [Test]
        public void Create_UnportedSuite_ThrowsNamingTheSuite()
        {
            var ex = Assert.Throws<CipherEngineNotImplementedException>(
                () => BlockCipherEngineFactory.Create(CipherSuite.Serpent, new byte[16]));
            StringAssert.Contains("Serpent", ex.Message);
        }

        [Test]
        public void TeaSession_ClientServer_RoundTrip()
        {
            // Both directions forced to TEA with distinct keys/IVs: proves the engine
            // plugs into CipherSession + CtrStream with per-direction material.
            var material = new CipherKeyMaterial
            {
                Direction0 = new CipherDirectionMaterial
                {
                    Suite = CipherSuite.TEA,
                    Key = FromHex("00000000000000000000000000000000"),
                    IV = FromHex("0000000000000000")
                },
                Direction1 = new CipherDirectionMaterial
                {
                    Suite = CipherSuite.TEA,
                    Key = FromHex("123456789ABCDEF0123456789ABCDEF0"),
                    IV = FromHex("0123456789ABCDEF")
                }
            };

            using (var client = new CipherSession(true, material, BlockCipherEngineFactory.ForSession()))
            using (var server = new CipherSession(false, material, BlockCipherEngineFactory.ForSession()))
            {
                client.SetActivated(true);
                server.SetActivated(true);

                byte[] payload = new byte[65]; // multi-block + partial tail
                for (int i = 0; i < payload.Length; i++)
                {
                    payload[i] = (byte)(i * 5 + 1);
                }
                byte[] original = (byte[])payload.Clone();

                client.Encrypt(payload, 0, payload.Length);
                Assert.AreNotEqual(original, payload);

                server.Decrypt(payload, 0, payload.Length);
                CollectionAssert.AreEqual(original, payload);

                // And back the other way (server encoder == client decoder material).
                server.Encrypt(payload, 0, payload.Length);
                client.Decrypt(payload, 0, payload.Length);
                CollectionAssert.AreEqual(original, payload);
            }
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
