namespace Metin2.Protocol.Security
{
    /// <summary>
    /// Seam for one block-cipher engine (the 15 CryptoPP ciphers are NOT yet
    /// ported — see docs/protocol/cipher-spec.md §6). Engines must implement
    /// raw single-block encryption; CTR wrapping lives in <see cref="CtrStream"/>.
    /// </summary>
    public interface IBlockCipherEngine
    {
        int BlockSize { get; }

        /// <summary>Encrypts one BlockSize block: keystreamBlock = E(counterBlock).</summary>
        void EncryptBlock(byte[] counter, int counterOffset, byte[] keystream, int keystreamOffset);
    }

    /// <summary>
    /// CTR stream transform over an <see cref="IBlockCipherEngine"/>:
    /// keystream = E(counter), counter starts at the IV and increments
    /// big-endian per block (NIST SP 800-38A). In-place, arbitrary lengths,
    /// no padding; encryption and decryption are the same operation, matching
    /// CryptoPP CTR_Mode usage (cipher.cpp:86-93).
    ///
    /// Counter byte order vs CryptoPP modes.cpp is UNVERIFIED (modes.cpp is not
    /// vendored — see cipher-spec.md §6); confirm against the first live
    /// encrypted packet before trusting traffic.
    /// </summary>
    public class CtrStream
    {
        private readonly IBlockCipherEngine _engine;
        private readonly byte[] _counter;
        private readonly byte[] _keystreamBlock;
        private int _keystreamPos;

        public CtrStream(IBlockCipherEngine engine, byte[] iv)
        {
            if (engine == null)
            {
                throw new System.ArgumentNullException(nameof(engine));
            }

            if (iv == null)
            {
                throw new System.ArgumentNullException(nameof(iv));
            }

            if (iv.Length != engine.BlockSize)
            {
                throw new System.ArgumentException(
                    $"IV length {iv.Length} must equal engine block size {engine.BlockSize}.",
                    nameof(iv));
            }

            _engine = engine;
            _counter = (byte[])iv.Clone();
            _keystreamBlock = new byte[engine.BlockSize];
            _keystreamPos = engine.BlockSize; // force keystream generation on first byte
        }

        /// <summary>
        /// XORs count bytes in place starting at offset (encrypt = decrypt).
        /// Stateful: the counter advances across calls, like ProcessData.
        /// </summary>
        public void ProcessData(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new System.ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(count));
            }

            int blockSize = _engine.BlockSize;
            for (int i = 0; i < count; i++)
            {
                if (_keystreamPos >= blockSize)
                {
                    _engine.EncryptBlock(_counter, 0, _keystreamBlock, 0);
                    IncrementCounter();
                    _keystreamPos = 0;
                }

                buffer[offset + i] ^= _keystreamBlock[_keystreamPos++];
            }
        }

        private void IncrementCounter()
        {
            for (int i = _counter.Length - 1; i >= 0; i--)
            {
                if (++_counter[i] != 0)
                {
                    break;
                }
            }
        }
    }
}
