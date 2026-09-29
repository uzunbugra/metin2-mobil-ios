using System;
using NUnit.Framework;
using Metin2.Protocol.Security;

namespace Metin2.Tests.EditMode.Protocol
{
    [TestFixture]
    public class CipherSessionTests
    {
        /// <summary>Test-only engine: keystream = counter XOR mask (NOT a real cipher).</summary>
        private sealed class StubEngine : IBlockCipherEngine
        {
            private readonly byte _mask;
            public int BlockSize { get; }

            public StubEngine(int blockSize, byte mask = 0x5A)
            {
                BlockSize = blockSize;
                _mask = mask;
            }

            public void EncryptBlock(byte[] counter, int counterOffset, byte[] keystream, int keystreamOffset)
            {
                for (int i = 0; i < BlockSize; i++)
                {
                    keystream[keystreamOffset + i] = (byte)(counter[counterOffset + i] ^ _mask);
                }
            }
        }

        private static CipherKeyMaterial TestMaterial()
        {
            // Sequential shared: Direction0 = Twofish/blk16, Direction1 = RC6/blk16.
            byte[] shared = new byte[256];
            for (int i = 0; i < shared.Length; i++)
            {
                shared[i] = (byte)i;
            }

            Assert.IsTrue(CipherKeyDerivation.TryDerive(shared, out CipherKeyMaterial material));
            return material;
        }

        private static IBlockCipherEngine EngineFor(CipherDirectionMaterial direction)
        {
            return new StubEngine(CipherSuiteTable.GetBlockSize(direction.Suite));
        }

        [Test]
        public void Polarity_ClientEncodesWithDirection1_ServerMirrors()
        {
            var material = TestMaterial();

            using (var client = new CipherSession(true, material, EngineFor))
            using (var server = new CipherSession(false, material, EngineFor))
            {
                // cipher.cpp:232-238
                Assert.AreEqual(material.Direction1.Suite, client.EncoderMaterial.Suite);
                Assert.AreEqual(material.Direction0.Suite, client.DecoderMaterial.Suite);
                Assert.AreEqual(material.Direction0.Suite, server.EncoderMaterial.Suite);
                Assert.AreEqual(material.Direction1.Suite, server.DecoderMaterial.Suite);

                // Cross-direction suites match: client encoder == server decoder.
                Assert.AreEqual(client.EncoderMaterial.Suite, server.DecoderMaterial.Suite);
                Assert.AreEqual(client.DecoderMaterial.Suite, server.EncoderMaterial.Suite);
            }
        }

        [Test]
        public void InactiveSession_EncryptDecrypt_AreSilentNoOps()
        {
            // Mirrors cipher.h: Encrypt/Decrypt return early unless activated.
            var material = TestMaterial();
            using (var session = new CipherSession(true, material, EngineFor))
            {
                byte[] data = new byte[] { 1, 2, 3, 4 };
                session.Encrypt(data, 0, data.Length);
                session.Decrypt(data, 0, data.Length);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, data);
            }
        }

        [Test]
        public void ActivatedWithoutEngine_ThrowsWithSpecPointer()
        {
            var material = TestMaterial();
            using (var session = new CipherSession(true, material))
            {
                var ex = Assert.Throws<InvalidOperationException>(() => session.SetActivated(true));
                StringAssert.Contains("cipher-spec", ex.Message);
            }
        }

        [Test]
        public void ClientServer_RoundTrip_WithMatchingPolarity()
        {
            var material = TestMaterial();
            using (var client = new CipherSession(true, material, EngineFor))
            using (var server = new CipherSession(false, material, EngineFor))
            {
                client.SetActivated(true);
                server.SetActivated(true);

                // 261-byte key-agreement-sized payload exercises multi-block + partial tail.
                byte[] payload = new byte[261];
                for (int i = 0; i < payload.Length; i++)
                {
                    payload[i] = (byte)(i * 7 + 3);
                }
                byte[] original = (byte[])payload.Clone();

                client.Encrypt(payload, 0, payload.Length);
                Assert.AreNotEqual(original, payload);

                server.Decrypt(payload, 0, payload.Length);
                CollectionAssert.AreEqual(original, payload);
            }
        }

        [Test]
        public void Ctr_CounterAdvancesBigEndian()
        {
            // Identity engine (mask 0x00): keystream IS the counter block.
            // IV = {0 x15, 0xFE}: block1 = IV+1 = {..., 0xFF},
            // block2 = IV+2 = {..., 0x01, 0x00} (carry propagates left = big-endian).
            var engine = new StubEngine(16, 0x00);
            byte[] iv = new byte[16];
            iv[15] = 0xFE;
            var stream = new CtrStream(engine, iv);

            byte[] data = new byte[48]; // 3 zero blocks
            stream.ProcessData(data, 0, data.Length);

            Assert.AreEqual(0xFE, data[15]); // block0 = IV
            Assert.AreEqual(0xFF, data[31]); // block1 last byte = 0xFE + 1
            Assert.AreEqual(0x00, data[32]); // block2 first byte unchanged
            Assert.AreEqual(0x01, data[46]); // carry propagated to byte 14
            Assert.AreEqual(0x00, data[47]); // low byte wrapped
        }

        [Test]
        public void Ctr_SplitCalls_EqualsSingleCall()
        {
            var material = TestMaterial();
            byte[] iv = material.Direction1.IV;

            byte[] single = new byte[20];
            for (int i = 0; i < single.Length; i++)
            {
                single[i] = (byte)(i + 1);
            }
            byte[] split = (byte[])single.Clone();

            new CtrStream(new StubEngine(iv.Length), iv).ProcessData(single, 0, single.Length);

            var splitStream = new CtrStream(new StubEngine(iv.Length), iv);
            splitStream.ProcessData(split, 0, 13);
            splitStream.ProcessData(split, 13, 7);

            CollectionAssert.AreEqual(single, split);
        }

        [Test]
        public void Ctr_RoundTrip_OddLength()
        {
            var engine = new StubEngine(8);
            byte[] iv = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 };
            byte[] data = new byte[13];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i * 11);
            }
            byte[] original = (byte[])data.Clone();

            new CtrStream(engine, iv).ProcessData(data, 0, data.Length);
            Assert.AreNotEqual(original, data);

            new CtrStream(engine, iv).ProcessData(data, 0, data.Length);
            CollectionAssert.AreEqual(original, data);
        }

        [Test]
        public void Ctr_LeavesBytesOutsideRangeUntouched()
        {
            var engine = new StubEngine(8);
            byte[] data = new byte[] { 0xAA, 0xBB, 1, 2, 3, 0xCC };
            new CtrStream(engine, new byte[8]).ProcessData(data, 2, 3);
            Assert.AreEqual(0xAA, data[0]);
            Assert.AreEqual(0xBB, data[1]);
            Assert.AreEqual(0xCC, data[5]);
        }

        [Test]
        public void Ctr_WrongIvLength_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CtrStream(new StubEngine(8), new byte[16]));
        }

        [Test]
        public void Session_CleanUp_Deactivates()
        {
            var material = TestMaterial();
            using (var session = new CipherSession(true, material, EngineFor))
            {
                session.SetActivated(true);
                Assert.IsTrue(session.Activated);
                session.CleanUp();
                Assert.IsFalse(session.Activated);

                byte[] data = new byte[] { 1, 2, 3 };
                session.Encrypt(data, 0, data.Length);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, data);
            }
        }
    }
}
