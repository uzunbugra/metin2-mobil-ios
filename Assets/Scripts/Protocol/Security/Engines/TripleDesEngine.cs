using System;

namespace Metin2.Protocol.Security.Engines
{
    /// <summary>
    /// 2-key Triple-DES (DES-EDE2): 64-bit block, 128-bit key (K1‖K2) —
    /// matching `DES_EDE2` as selected by cipher.cpp:269-271
    /// (`DES_EDE2_Info`: block 8, `FixedKeyLength&lt;16&gt;`, cryptopp/des.h).
    ///
    /// Textbook DES core from the FIPS 46-3 tables (IP/FP/E/P/PC1/PC2/rotations/
    /// S-boxes; PC-1 cross-checked against Wikipedia's DES supplementary material
    /// after a transcription slip dropped entry "21" — see TripleDesEngineTests).
    /// EDE wiring mirrors CryptoPP DES_EDE2::Base (m_des1/m_des2): encrypt =
    /// DESenc(K1) → DESdec(K2) → DESenc(K1). Parity bits ignored (as CryptoPP).
    /// Verified against NIST SP 800-17 vectors + Rivest Destest + PyCryptodome
    /// 2-key vector + Python cross-checks (see tests).
    /// No UnityEngine dependency.
    /// </summary>
    public class TripleDesEngine : IBlockCipherEngine
    {
        public const int KeyLength = 16;

        public int BlockSize => 8;

        private readonly ulong[] _k1 = new ulong[16];
        private readonly ulong[] _k2 = new ulong[16];

        private static readonly int[] IP = new int[]
        {
            58, 50, 42, 34, 26, 18, 10, 2,
            60, 52, 44, 36, 28, 20, 12, 4,
            62, 54, 46, 38, 30, 22, 14, 6,
            64, 56, 48, 40, 32, 24, 16, 8,
            57, 49, 41, 33, 25, 17, 9, 1,
            59, 51, 43, 35, 27, 19, 11, 3,
            61, 53, 45, 37, 29, 21, 13, 5,
            63, 55, 47, 39, 31, 23, 15, 7
        };

        private static readonly int[] FP = new int[]
        {
            40, 8, 48, 16, 56, 24, 64, 32,
            39, 7, 47, 15, 55, 23, 63, 31,
            38, 6, 46, 14, 54, 22, 62, 30,
            37, 5, 45, 13, 53, 21, 61, 29,
            36, 4, 44, 12, 52, 20, 60, 28,
            35, 3, 43, 11, 51, 19, 59, 27,
            34, 2, 42, 10, 50, 18, 58, 26,
            33, 1, 41, 9, 49, 17, 57, 25
        };

        private static readonly int[] E = new int[]
        {
            32, 1, 2, 3, 4, 5,
            4, 5, 6, 7, 8, 9,
            8, 9, 10, 11, 12, 13,
            12, 13, 14, 15, 16, 17,
            16, 17, 18, 19, 20, 21,
            20, 21, 22, 23, 24, 25,
            24, 25, 26, 27, 28, 29,
            28, 29, 30, 31, 32, 1
        };

        private static readonly int[] P = new int[]
        {
            16, 7, 20, 21, 29, 12, 28, 17,
            1, 15, 23, 26, 5, 18, 31, 10,
            2, 8, 24, 14, 32, 27, 3, 9,
            19, 13, 30, 6, 22, 11, 4, 25
        };

        private static readonly int[] PC1 = new int[]
        {
            57, 49, 41, 33, 25, 17, 9,
            1, 58, 50, 42, 34, 26, 18,
            10, 2, 59, 51, 43, 35, 27,
            19, 11, 3, 60, 52, 44, 36,
            63, 55, 47, 39, 31, 23, 15,
            7, 62, 54, 46, 38, 30, 22,
            14, 6, 61, 53, 45, 37, 29,
            21, 13, 5, 28, 20, 12, 4
        };

        private static readonly int[] PC2 = new int[]
        {
            14, 17, 11, 24, 1, 5,
            3, 28, 15, 6, 21, 10,
            23, 19, 12, 4, 26, 8,
            16, 7, 27, 20, 13, 2,
            41, 52, 31, 37, 47, 55,
            30, 40, 51, 45, 33, 48,
            44, 49, 39, 56, 34, 53,
            46, 42, 50, 36, 29, 32
        };

        private static readonly int[] Rotations = new int[]
        {
            1, 1, 2, 2, 2, 2, 2, 2, 1, 2, 2, 2, 2, 2, 2, 1
        };

        // S1..S8 flattened (FIPS 46-3 §3.3): index = box * 64 + row * 16 + col.
        private static readonly int[] S = new int[]
        {
            14, 4, 13, 1, 2, 15, 11, 8, 3, 10, 6, 12, 5, 9, 0, 7,
            0, 15, 7, 4, 14, 2, 13, 1, 10, 6, 12, 11, 9, 5, 3, 8,
            4, 1, 14, 8, 13, 6, 2, 11, 15, 12, 9, 7, 3, 10, 5, 0,
            15, 12, 8, 2, 4, 9, 1, 7, 5, 11, 3, 14, 10, 0, 6, 13,
            15, 1, 8, 14, 6, 11, 3, 4, 9, 7, 2, 13, 12, 0, 5, 10,
            3, 13, 4, 7, 15, 2, 8, 14, 12, 0, 1, 10, 6, 9, 11, 5,
            0, 14, 7, 11, 10, 4, 13, 1, 5, 8, 12, 6, 9, 3, 2, 15,
            13, 8, 10, 1, 3, 15, 4, 2, 11, 6, 7, 12, 0, 5, 14, 9,
            10, 0, 9, 14, 6, 3, 15, 5, 1, 13, 12, 7, 11, 4, 2, 8,
            13, 7, 0, 9, 3, 4, 6, 10, 2, 8, 5, 14, 12, 11, 15, 1,
            13, 6, 4, 9, 8, 15, 3, 0, 11, 1, 2, 12, 5, 10, 14, 7,
            1, 10, 13, 0, 6, 9, 8, 7, 4, 15, 14, 3, 11, 5, 2, 12,
            7, 13, 14, 3, 0, 6, 9, 10, 1, 2, 8, 5, 11, 12, 4, 15,
            13, 8, 11, 5, 6, 15, 0, 3, 4, 7, 2, 12, 1, 10, 14, 9,
            10, 6, 9, 0, 12, 11, 7, 13, 15, 1, 3, 14, 5, 2, 8, 4,
            3, 15, 0, 6, 10, 1, 13, 8, 9, 4, 5, 11, 12, 7, 2, 14,
            2, 12, 4, 1, 7, 10, 11, 6, 8, 5, 3, 15, 13, 0, 14, 9,
            14, 11, 2, 12, 4, 7, 13, 1, 5, 0, 15, 10, 3, 9, 8, 6,
            4, 2, 1, 11, 10, 13, 7, 8, 15, 9, 12, 5, 6, 3, 0, 14,
            11, 8, 12, 7, 1, 14, 2, 13, 6, 15, 0, 9, 10, 4, 5, 3,
            12, 1, 10, 15, 9, 2, 6, 8, 0, 13, 3, 4, 14, 7, 5, 11,
            10, 15, 4, 2, 7, 12, 9, 5, 6, 1, 13, 14, 0, 11, 3, 8,
            9, 14, 15, 5, 2, 8, 12, 3, 7, 0, 4, 10, 1, 13, 11, 6,
            4, 3, 2, 12, 9, 5, 15, 10, 11, 14, 1, 7, 6, 0, 8, 13,
            4, 11, 2, 14, 15, 0, 8, 13, 3, 12, 9, 7, 5, 10, 6, 1,
            13, 0, 11, 7, 4, 9, 1, 10, 14, 3, 5, 12, 2, 15, 8, 6,
            1, 4, 11, 13, 12, 3, 7, 14, 10, 15, 6, 8, 0, 5, 9, 2,
            6, 11, 13, 8, 1, 4, 10, 7, 9, 5, 0, 15, 14, 2, 3, 12,
            13, 2, 8, 4, 6, 15, 11, 1, 10, 9, 3, 14, 5, 0, 12, 7,
            1, 15, 13, 8, 10, 3, 7, 4, 12, 5, 6, 11, 0, 14, 9, 2,
            7, 11, 4, 1, 9, 12, 14, 2, 0, 6, 10, 13, 15, 3, 5, 8,
            2, 1, 14, 7, 4, 10, 8, 13, 15, 12, 9, 0, 3, 5, 6, 11
        };

        public TripleDesEngine(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length != KeyLength)
            {
                throw new ArgumentException(
                    $"DES-EDE2 key must be {KeyLength} bytes, got {key.Length}.",
                    nameof(key));
            }

            ExpandKey(key, 0, _k1);
            ExpandKey(key, 8, _k2);
        }

        public void EncryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            ulong block = LoadBigEndian(input, inputOffset);
            block = Permute(block, 64, IP);
            uint left = (uint)(block >> 32);
            uint right = (uint)block;

            DesRounds(ref left, ref right, _k1, true);
            DesRounds(ref left, ref right, _k2, false);
            DesRounds(ref left, ref right, _k1, true);

            StoreBigEndian(output, outputOffset, Permute(((ulong)left << 32) | right, 64, FP));
        }

        /// <summary>DES-EDE2 decryption (KAT verification; CTR mode itself only uses encryption).</summary>
        public void DecryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            ulong block = LoadBigEndian(input, inputOffset);
            block = Permute(block, 64, IP);
            uint left = (uint)(block >> 32);
            uint right = (uint)block;

            DesRounds(ref left, ref right, _k1, false);
            DesRounds(ref left, ref right, _k2, true);
            DesRounds(ref left, ref right, _k1, false);

            StoreBigEndian(output, outputOffset, Permute(((ulong)left << 32) | right, 64, FP));
        }

        /// <summary>16 Feistel rounds + final swap (FP applied by the caller).</summary>
        private static void DesRounds(ref uint left, ref uint right, ulong[] subkeys, bool encrypt)
        {
            for (int i = 0; i < 16; i++)
            {
                ulong k = encrypt ? subkeys[i] : subkeys[15 - i];
                uint next = left ^ F(right, k);
                left = right;
                right = next;
            }

            uint temp = left;
            left = right;
            right = temp;
        }

        private static uint F(uint r, ulong subkey)
        {
            ulong e = Permute(r, 32, E) ^ subkey;
            uint o = 0;
            for (int i = 0; i < 8; i++)
            {
                uint group = (uint)((e >> (42 - 6 * i)) & 0x3FUL);
                uint row = ((group >> 5) << 1) | (group & 1U);
                uint col = (group >> 1) & 0xFU;
                o = (o << 4) | (uint)S[i * 64 + row * 16 + col];
            }

            return (uint)Permute(o, 32, P);
        }

        private static void ExpandKey(byte[] key, int offset, ulong[] subkeys)
        {
            ulong k = 0;
            for (int i = 0; i < 8; i++)
            {
                k = (k << 8) | key[offset + i];
            }

            ulong cd = Permute(k, 64, PC1);
            uint c = (uint)(cd >> 28) & 0xFFFFFFFU;
            uint d = (uint)cd & 0xFFFFFFFU;

            for (int i = 0; i < 16; i++)
            {
                c = RotateLeft28(c, Rotations[i]);
                d = RotateLeft28(d, Rotations[i]);
                subkeys[i] = Permute(((ulong)c << 28) | d, 56, PC2);
            }
        }

        private static uint RotateLeft28(uint value, int shift)
        {
            return ((value << shift) | (value >> (28 - shift))) & 0xFFFFFFFU;
        }

        /// <summary>MSB-first permutation: output bit (i+1) = input bit table[i].</summary>
        private static ulong Permute(ulong x, int inBits, int[] table)
        {
            ulong y = 0;
            for (int i = 0; i < table.Length; i++)
            {
                y <<= 1;
                y |= (x >> (inBits - table[i])) & 1UL;
            }

            return y;
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

        private static ulong LoadBigEndian(byte[] data, int offset)
        {
            return ((ulong)data[offset] << 56)
                | ((ulong)data[offset + 1] << 48)
                | ((ulong)data[offset + 2] << 40)
                | ((ulong)data[offset + 3] << 32)
                | ((ulong)data[offset + 4] << 24)
                | ((ulong)data[offset + 5] << 16)
                | ((ulong)data[offset + 6] << 8)
                | data[offset + 7];
        }

        private static void StoreBigEndian(byte[] data, int offset, ulong value)
        {
            data[offset] = (byte)(value >> 56);
            data[offset + 1] = (byte)(value >> 48);
            data[offset + 2] = (byte)(value >> 40);
            data[offset + 3] = (byte)(value >> 32);
            data[offset + 4] = (byte)(value >> 24);
            data[offset + 5] = (byte)(value >> 16);
            data[offset + 6] = (byte)(value >> 8);
            data[offset + 7] = (byte)value;
        }
    }
}
