using System;

namespace Metin2.Protocol.Security.Engines
{
    /// <summary>
    /// IDEA (Lai–Massey, 8 rounds + output transform): 64-bit block, 128-bit key —
    /// matching `IDEA` as selected by cipher.cpp:266-268
    /// (`IDEA_Info`: block 8, key 16, rounds 8, cryptopp/idea.h:16).
    ///
    /// Words are big-endian; multiplication is mod (2^16+1) with the zero→2^16
    /// convention. Verified against HAC Table 7.12 + reference-implementation
    /// vectors (see IdeaEngineTests).
    /// No UnityEngine dependency.
    /// </summary>
    public class IdeaEngine : IBlockCipherEngine
    {
        public const int KeyLength = 16;

        private const int Rounds = 8;
        private const int Subkeys = 52;

        private readonly ushort[] _encKeys = new ushort[Subkeys];

        public int BlockSize => 8;

        public IdeaEngine(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length != KeyLength)
            {
                throw new ArgumentException(
                    $"IDEA key must be {KeyLength} bytes, got {key.Length}.",
                    nameof(key));
            }

            ExpandEncryptionKeys(key);
        }

        public void EncryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);
            Crypt(input, inputOffset, output, outputOffset, _encKeys);
        }

        /// <summary>
        /// Explicit inverse (separate path, like CryptoPP's TEA::Dec): undoes the
        /// crossed output transform, then inverts rounds 8..1 each with its OWN
        /// keys. MA inputs are recoverable from the output (o1^o2, o3^o4), so no
        /// cross-round key staggering is needed. Each step rigorously inverts the
        /// corresponding EncryptBlock step.
        /// </summary>
        public void DecryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            ushort s1 = Mul(LoadBigEndian(input, inputOffset), MulInverse(_encKeys[48]));
            ushort s3 = Add(LoadBigEndian(input, inputOffset + 2), AddInverse(_encKeys[49]));
            ushort s2 = Add(LoadBigEndian(input, inputOffset + 4), AddInverse(_encKeys[50]));
            ushort s4 = Mul(LoadBigEndian(input, inputOffset + 6), MulInverse(_encKeys[51]));

            for (int r = Rounds - 1; r >= 0; r--)
            {
                int b = r * 6;

                ushort u = Mul((ushort)(s1 ^ s2), _encKeys[b + 4]);
                ushort v = Add((ushort)(s3 ^ s4), u);
                v = Mul(v, _encKeys[b + 5]);
                u = Add(u, v);

                ushort h1 = (ushort)(s1 ^ v);
                ushort h3 = (ushort)(s2 ^ v);
                ushort h2 = (ushort)(s3 ^ u);
                ushort h4 = (ushort)(s4 ^ u);

                s1 = Mul(h1, MulInverse(_encKeys[b]));
                s2 = Add(h2, AddInverse(_encKeys[b + 1]));
                s3 = Add(h3, AddInverse(_encKeys[b + 2]));
                s4 = Mul(h4, MulInverse(_encKeys[b + 3]));
            }

            StoreBigEndian(output, outputOffset, s1);
            StoreBigEndian(output, outputOffset + 2, s2);
            StoreBigEndian(output, outputOffset + 4, s3);
            StoreBigEndian(output, outputOffset + 6, s4);
        }

        /// <summary>
        /// Test seam: exposes the 52-word encryption key schedule for verification
        /// against published subkey tables (e.g., HAC Table 7.12).
        /// </summary>
        public ushort[] GetEncryptionKeysForTest()
        {
            return (ushort[])_encKeys.Clone();
        }

        private static void Crypt(byte[] input, int inputOffset, byte[] output, int outputOffset, ushort[] keys)
        {
            ushort x1 = LoadBigEndian(input, inputOffset);
            ushort x2 = LoadBigEndian(input, inputOffset + 2);
            ushort x3 = LoadBigEndian(input, inputOffset + 4);
            ushort x4 = LoadBigEndian(input, inputOffset + 6);

            int k = 0;
            for (int round = 0; round < Rounds; round++)
            {
                ushort y1 = Mul(x1, keys[k++]);
                ushort y2 = Add(x2, keys[k++]);
                ushort y3 = Add(x3, keys[k++]);
                ushort y4 = Mul(x4, keys[k++]);

                ushort t0 = Mul((ushort)(y1 ^ y3), keys[k++]);
                ushort t1 = Add((ushort)(y2 ^ y4), t0);
                t1 = Mul(t1, keys[k++]);
                t0 = Add(t0, t1);

                x1 = (ushort)(y1 ^ t1);
                x4 = (ushort)(y4 ^ t0);
                // (11)=y1^t1, (12)=y3^t1, (13)=y2^t0, (14)=y4^t0.
                // Verified by hand-trace against HAC Table 7.12: rounds chain
                // ((11),(12),(13),(14)) directly into the next round.
                x2 = (ushort)(y3 ^ t1);
                x3 = (ushort)(y2 ^ t0);
            }

            // Output transform consumes the inner words CROSSED (Y2 from X3, Y3
            // from X2): the cipher's single word-swap lives here, not between
            // rounds. Proven by full 8-round hand/Python trace against HAC
            // Table 7.12 (rounds chain ((11),(12),(13),(14)) straight through).
            StoreBigEndian(output, outputOffset, Mul(x1, keys[k++]));
            StoreBigEndian(output, outputOffset + 2, Add(x3, keys[k++]));
            StoreBigEndian(output, outputOffset + 4, Add(x2, keys[k++]));
            StoreBigEndian(output, outputOffset + 6, Mul(x4, keys[k++]));
        }

        private void ExpandEncryptionKeys(byte[] key)
        {
            // First 8 subkeys are the key words; then rotate the 128-bit key
            // left by 25 bits per group (25 = 3 bytes + 1 bit).
            byte[] current = (byte[])key.Clone();
            int k = 0;
            for (int group = 0; group < 7 && k < Subkeys; group++)
            {
                for (int i = 0; i < 8 && k < Subkeys; i++)
                {
                    _encKeys[k++] = LoadBigEndian(current, i * 2);
                }

                byte[] rotated = new byte[16];
                for (int i = 0; i < 16; i++)
                {
                    rotated[i] = (byte)((current[(i + 3) % 16] << 1) | (current[(i + 4) % 16] >> 7));
                }
                current = rotated;
            }
        }

        private static ushort Mul(ushort a, ushort b)
        {
            ulong x = a == 0 ? 0x10000uL : a;
            ulong y = b == 0 ? 0x10000uL : b;
            ulong r = (x * y) % 0x10001uL;
            return (ushort)(r == 0x10000uL ? 0 : r);
        }

        private static ushort Add(ushort a, ushort b)
        {
            return (ushort)((a + b) & 0xFFFF);
        }

        private static ushort AddInverse(ushort x)
        {
            return (ushort)((0x10000 - x) & 0xFFFF);
        }

        private static ushort MulInverse(ushort x)
        {
            if (x == 0)
            {
                return 0;
            }

            // Extended Euclidean algorithm for inverse mod (2^16+1).
            long t0 = 0;
            long t1 = 1;
            long r0 = 0x10001;
            long r1 = x;
            while (r1 != 0)
            {
                long q = r0 / r1;
                long t2 = t0 - q * t1;
                t0 = t1;
                t1 = t2;
                long r2 = r0 - q * r1;
                r0 = r1;
                r1 = r2;
            }

            if (t0 < 0)
            {
                t0 += 0x10001;
            }

            return (ushort)t0;
        }

        private static void CheckArgs(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (inputOffset < 0 || inputOffset + 8 > input.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputOffset));
            }

            if (outputOffset < 0 || outputOffset + 8 > output.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(outputOffset));
            }
        }

        private static ushort LoadBigEndian(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static void StoreBigEndian(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)value;
        }
    }
}
