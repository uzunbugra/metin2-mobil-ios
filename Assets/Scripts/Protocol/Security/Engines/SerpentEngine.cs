using System;

namespace Metin2.Protocol.Security.Engines
{
    /// <summary>
    /// Serpent (Anderson–Biham–Knudsen, AES finalist): 128-bit block, 32 rounds,
    /// 128/192/256-bit key — matching `Serpent` as selected by cipher.cpp:257-259
    /// (`Serpent_Info`: block 16, `VariableKeyLength&lt;16, 16, 32, 8&gt;`,
    /// `FixedRounds&lt;32&gt;`, cryptopp/serpent.h).
    ///
    /// Classic (non-bitsliced) structure per the submission spec, with the Osvik
    /// bitsliced S-box boolean functions (Dag Arne Osvik, "Speeding up Serpent";
    /// via libgcrypt serpent.c, LGPL — algorithm itself public domain) applied
    /// nibble-parallel across the four state words, exactly as the spec's get_sk
    /// formulation: input bit j of w0..w3 selects the S-box entry, output bits
    /// scatter back to bit j. Key schedule: LE words + "1"-bit padding, golden-ratio
    /// prekey recurrence, 33 subkey groups with S[(35-g)%8]. LT/ILT in uint ops.
    /// Verified against Botan serpent.vec + libgcrypt self-test vectors
    /// (see SerpentEngineTests).
    /// No UnityEngine dependency.
    /// </summary>
    public class SerpentEngine : IBlockCipherEngine
    {
        public const int MinKeyLength = 16;
        public const int MaxKeyLength = 32;

        private const int Rounds = 32;
        private const uint Phi = 0x9E3779B9;

        public int BlockSize => 16;

        // 33 subkey groups × 4 words.
        private readonly uint[][] _sk = new uint[33][];

        public SerpentEngine(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
            {
                throw new ArgumentException(
                    $"Serpent key must be 16, 24 or 32 bytes, got {key.Length}.",
                    nameof(key));
            }

            for (int i = 0; i < 33; i++)
            {
                _sk[i] = new uint[4];
            }

            // Padded LE key words: short keys get a single "1" bit then zeros.
            uint[] k0 = new uint[8];
            byte[] padded = new byte[32];
            System.Buffer.BlockCopy(key, 0, padded, 0, key.Length);
            for (int i = 0; i < 8; i++)
            {
                k0[i] = LoadLittleEndian(padded, i * 4);
            }

            if (key.Length < 32)
            {
                k0[key.Length / 4] |= 1U << ((key.Length % 4) * 8);
            }

            // Prekeys: w[0..7] = padded key seed, then golden-ratio recurrence.
            // NOTE: the recurrence output packs AFTER the seed (w[8+j], constant j);
            // subkey groups read w[8+4g..8+4g+3] — not w[4g..] (that would feed raw
            // key words into groups 0-1).
            uint[] w = new uint[8 + 132];
            for (int i = 0; i < 8; i++)
            {
                w[i] = k0[i];
            }

            for (int j = 0; j < 132; j++)
            {
                w[8 + j] = RotateLeft(w[j] ^ w[j + 3] ^ w[j + 5] ^ w[j + 7] ^ Phi ^ (uint)j, 11);
            }

            // 33 subkey groups: group g takes prekeys w[8+4g..8+4g+3] through S[(35-g)%8].
            for (int g = 0; g < 33; g++)
            {
                uint[] ws = new uint[] { w[8 + 4 * g], w[8 + 4 * g + 1], w[8 + 4 * g + 2], w[8 + 4 * g + 3] };
                ApplySbox((35 - g) % 8, ws, _sk[g]);
            }
        }

        public void EncryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            uint[] b = new uint[]
            {
                LoadLittleEndian(input, inputOffset),
                LoadLittleEndian(input, inputOffset + 4),
                LoadLittleEndian(input, inputOffset + 8),
                LoadLittleEndian(input, inputOffset + 12)
            };
            uint[] t = new uint[4];

            for (int round = 0; round < 31; round++)
            {
                XorBlock(b, _sk[round]);
                ApplySbox(round % 8, b, t);
                LinearTransform(t);
                CopyBlock(b, t);
            }

            // Last round (S7): no linear transform, extra key mixing.
            XorBlock(b, _sk[31]);
            ApplySbox(7, b, t);
            XorBlock(t, _sk[32]);

            StoreLittleEndian(output, outputOffset, t[0]);
            StoreLittleEndian(output, outputOffset + 4, t[1]);
            StoreLittleEndian(output, outputOffset + 8, t[2]);
            StoreLittleEndian(output, outputOffset + 12, t[3]);
        }

        /// <summary>Serpent decryption (KAT verification; CTR mode itself only uses encryption).</summary>
        public void DecryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            uint[] b = new uint[]
            {
                LoadLittleEndian(input, inputOffset),
                LoadLittleEndian(input, inputOffset + 4),
                LoadLittleEndian(input, inputOffset + 8),
                LoadLittleEndian(input, inputOffset + 12)
            };
            uint[] t = new uint[4];

            // Inverse of the last round first.
            XorBlock(b, _sk[32]);
            ApplyInverseSbox(7, b, t);
            XorBlock(t, _sk[31]);
            CopyBlock(b, t);

            for (int round = 30; round >= 0; round--)
            {
                InverseLinearTransform(b);
                ApplyInverseSbox(round % 8, b, t);
                XorBlock(t, _sk[round]);
                CopyBlock(b, t);
            }

            StoreLittleEndian(output, outputOffset, b[0]);
            StoreLittleEndian(output, outputOffset + 4, b[1]);
            StoreLittleEndian(output, outputOffset + 8, b[2]);
            StoreLittleEndian(output, outputOffset + 12, b[3]);
        }

        /// <summary>
        /// Bitslice S-box layer (Osvik boolean functions): input bit j of in[0..3]
        /// selects the entry, output bits scatter back to bit j of out[0..3].
        /// </summary>
        private static void ApplySbox(int which, uint[] input, uint[] output)
        {
            uint r0 = input[0];
            uint r1 = input[1];
            uint r2 = input[2];
            uint r3 = input[3];
            uint r4;
            uint w;
            uint x;
            uint y;
            uint z;

            switch (which)
            {
                case 0:
                    r3 ^= r0; r4 = r1;
                    r1 &= r3; r4 ^= r2;
                    r1 ^= r0; r0 |= r3;
                    r0 ^= r4; r4 ^= r3;
                    r3 ^= r2; r2 |= r1;
                    r2 ^= r4; r4 = ~r4;
                    r4 |= r1; r1 ^= r3;
                    r1 ^= r4; r3 |= r0;
                    r1 ^= r3; r4 ^= r3;
                    w = r1; x = r4; y = r2; z = r0;
                    break;
                case 1:
                    r0 = ~r0; r2 = ~r2;
                    r4 = r0; r0 &= r1;
                    r2 ^= r0; r0 |= r3;
                    r3 ^= r2; r1 ^= r0;
                    r0 ^= r4; r4 |= r1;
                    r1 ^= r3; r2 |= r0;
                    r2 &= r4; r0 ^= r1;
                    r1 &= r2;
                    r1 ^= r0; r0 &= r2;
                    r0 ^= r4;
                    w = r2; x = r0; y = r3; z = r1;
                    break;
                case 2:
                    r4 = r0; r0 &= r2;
                    r0 ^= r3; r2 ^= r1;
                    r2 ^= r0; r3 |= r4;
                    r3 ^= r1; r4 ^= r2;
                    r1 = r3; r3 |= r4;
                    r3 ^= r0; r0 &= r1;
                    r4 ^= r0; r1 ^= r3;
                    r1 ^= r4; r4 = ~r4;
                    w = r2; x = r3; y = r1; z = r4;
                    break;
                case 3:
                    r4 = r0; r0 |= r3;
                    r3 ^= r1; r1 &= r4;
                    r4 ^= r2; r2 ^= r3;
                    r3 &= r0; r4 |= r1;
                    r3 ^= r4; r0 ^= r1;
                    r4 &= r0; r1 ^= r3;
                    r4 ^= r2; r1 |= r0;
                    r1 ^= r2; r0 ^= r3;
                    r2 = r1; r1 |= r3;
                    r1 ^= r0;
                    w = r1; x = r2; y = r3; z = r4;
                    break;
                case 4:
                    r1 ^= r3; r3 = ~r3;
                    r2 ^= r3; r3 ^= r0;
                    r4 = r1; r1 &= r3;
                    r1 ^= r2; r4 ^= r3;
                    r0 ^= r4; r2 &= r4;
                    r2 ^= r0; r0 &= r1;
                    r3 ^= r0; r4 |= r1;
                    r4 ^= r0; r0 |= r3;
                    r0 ^= r2; r2 &= r3;
                    r0 = ~r0; r4 ^= r2;
                    w = r1; x = r4; y = r0; z = r3;
                    break;
                case 5:
                    r0 ^= r1; r1 ^= r3;
                    r3 = ~r3; r4 = r1;
                    r1 &= r0; r2 ^= r3;
                    r1 ^= r2; r2 |= r4;
                    r4 ^= r3; r3 &= r1;
                    r3 ^= r0; r4 ^= r1;
                    r4 ^= r2; r2 ^= r0;
                    r0 &= r3; r2 = ~r2;
                    r0 ^= r4; r4 |= r3;
                    r2 ^= r4;
                    w = r1; x = r3; y = r0; z = r2;
                    break;
                case 6:
                    r2 = ~r2; r4 = r3;
                    r3 &= r0; r0 ^= r4;
                    r3 ^= r2; r2 |= r4;
                    r1 ^= r3; r2 ^= r0;
                    r0 |= r1; r2 ^= r1;
                    r4 ^= r0; r0 |= r3;
                    r0 ^= r2; r4 ^= r3;
                    r4 ^= r0; r3 = ~r3;
                    r2 &= r4;
                    r2 ^= r3;
                    w = r0; x = r1; y = r4; z = r2;
                    break;
                default:
                    r4 = r2; r2 &= r1;
                    r2 ^= r3; r3 &= r1;
                    r4 ^= r2; r2 ^= r1;
                    r1 ^= r0; r0 |= r4;
                    r0 ^= r2; r3 ^= r1;
                    r2 ^= r3; r3 &= r0;
                    r3 ^= r4; r4 ^= r2;
                    r2 &= r0; r4 = ~r4;
                    r2 ^= r4; r4 &= r0;
                    r1 ^= r3; r4 ^= r1;
                    w = r2; x = r4; y = r3; z = r0;
                    break;
            }

            output[0] = w;
            output[1] = x;
            output[2] = y;
            output[3] = z;
        }

        private static void ApplyInverseSbox(int which, uint[] input, uint[] output)
        {
            uint r0 = input[0];
            uint r1 = input[1];
            uint r2 = input[2];
            uint r3 = input[3];
            uint r4;
            uint w;
            uint x;
            uint y;
            uint z;

            switch (which)
            {
                case 0:
                    r2 = ~r2; r4 = r1;
                    r1 |= r0; r4 = ~r4;
                    r1 ^= r2; r2 |= r4;
                    r1 ^= r3; r0 ^= r4;
                    r2 ^= r0; r0 &= r3;
                    r4 ^= r0; r0 |= r1;
                    r0 ^= r2; r3 ^= r4;
                    r2 ^= r1; r3 ^= r0;
                    r3 ^= r1;
                    r2 &= r3;
                    r4 ^= r2;
                    w = r0; x = r4; y = r1; z = r3;
                    break;
                case 1:
                    r4 = r1; r1 ^= r3;
                    r3 &= r1; r4 ^= r2;
                    r3 ^= r0; r0 |= r1;
                    r2 ^= r3; r0 ^= r4;
                    r0 |= r2; r1 ^= r3;
                    r0 ^= r1; r1 |= r3;
                    r1 ^= r0; r4 = ~r4;
                    r4 ^= r1; r1 |= r0;
                    r1 ^= r0;
                    r1 |= r4;
                    r3 ^= r1;
                    w = r4; x = r0; y = r3; z = r2;
                    break;
                case 2:
                    r2 ^= r3; r3 ^= r0;
                    r4 = r3; r3 &= r2;
                    r3 ^= r1; r1 |= r2;
                    r1 ^= r4; r4 &= r3;
                    r2 ^= r3; r4 &= r0;
                    r4 ^= r2; r2 &= r1;
                    r2 |= r0; r3 = ~r3;
                    r2 ^= r3; r0 ^= r3;
                    r0 &= r1; r3 ^= r4;
                    r3 ^= r0;
                    w = r1; x = r4; y = r2; z = r3;
                    break;
                case 3:
                    r4 = r2; r2 ^= r1;
                    r1 &= r2; r1 ^= r0;
                    r0 &= r4; r4 ^= r3;
                    r3 |= r1; r3 ^= r2;
                    r0 ^= r4; r2 ^= r0;
                    r0 |= r3; r0 ^= r1;
                    r4 ^= r2; r2 &= r3;
                    r1 |= r3; r1 ^= r2;
                    r4 ^= r0; r2 ^= r4;
                    w = r3; x = r0; y = r2; z = r1;
                    break;
                case 4:
                    r4 = r2; r2 &= r3;
                    r2 ^= r1; r1 |= r3;
                    r1 &= r0; r4 ^= r2;
                    r4 ^= r1; r1 &= r2;
                    r0 = ~r0; r3 ^= r4;
                    r1 ^= r3; r3 &= r0;
                    r3 ^= r2; r0 ^= r1;
                    r2 &= r0; r3 ^= r0;
                    r2 ^= r4;
                    r2 |= r3; r3 ^= r0;
                    r2 ^= r1;
                    w = r0; x = r3; y = r2; z = r4;
                    break;
                case 5:
                    r1 = ~r1; r4 = r3;
                    r2 ^= r1; r3 |= r0;
                    r3 ^= r2; r2 |= r1;
                    r2 &= r0; r4 ^= r3;
                    r2 ^= r4; r4 |= r0;
                    r4 ^= r1; r1 &= r2;
                    r1 ^= r3; r4 ^= r2;
                    r3 &= r4; r4 ^= r1;
                    r3 ^= r0; r3 ^= r4;
                    r4 = ~r4;
                    w = r1; x = r4; y = r3; z = r2;
                    break;
                case 6:
                    r0 ^= r2; r4 = r2;
                    r2 &= r0; r4 ^= r3;
                    r2 = ~r2; r3 ^= r1;
                    r2 ^= r3; r4 |= r0;
                    r0 ^= r2; r3 ^= r4;
                    r4 ^= r1; r1 &= r3;
                    r1 ^= r0; r0 ^= r3;
                    r0 |= r2; r3 ^= r1;
                    r4 ^= r0;
                    w = r1; x = r2; y = r4; z = r3;
                    break;
                default:
                    r4 = r2; r2 ^= r0;
                    r0 &= r3; r2 = ~r2;
                    r4 |= r3; r3 ^= r1;
                    r1 |= r0; r0 ^= r2;
                    r2 &= r4; r1 ^= r2;
                    r2 ^= r0; r0 |= r2;
                    r3 &= r4; r0 ^= r3;
                    r4 ^= r1; r3 ^= r4;
                    r4 |= r0; r3 ^= r2;
                    r4 ^= r2;
                    w = r3; x = r0; y = r1; z = r4;
                    break;
            }

            output[0] = w;
            output[1] = x;
            output[2] = y;
            output[3] = z;
        }

        private static void LinearTransform(uint[] b)
        {
            b[0] = RotateLeft(b[0], 13);
            b[2] = RotateLeft(b[2], 3);
            b[1] = b[1] ^ b[0] ^ b[2];
            b[3] = b[3] ^ b[2] ^ (b[0] << 3);
            b[1] = RotateLeft(b[1], 1);
            b[3] = RotateLeft(b[3], 7);
            b[0] = b[0] ^ b[1] ^ b[3];
            b[2] = b[2] ^ b[3] ^ (b[1] << 7);
            b[0] = RotateLeft(b[0], 5);
            b[2] = RotateLeft(b[2], 22);
        }

        private static void InverseLinearTransform(uint[] b)
        {
            b[2] = RotateRight(b[2], 22);
            b[0] = RotateRight(b[0], 5);
            b[2] = b[2] ^ b[3] ^ (b[1] << 7);
            b[0] = b[0] ^ b[1] ^ b[3];
            b[3] = RotateRight(b[3], 7);
            b[1] = RotateRight(b[1], 1);
            b[3] = b[3] ^ b[2] ^ (b[0] << 3);
            b[1] = b[1] ^ b[0] ^ b[2];
            b[2] = RotateRight(b[2], 3);
            b[0] = RotateRight(b[0], 13);
        }

        private static void XorBlock(uint[] block, uint[] key)
        {
            block[0] ^= key[0];
            block[1] ^= key[1];
            block[2] ^= key[2];
            block[3] ^= key[3];
        }

        private static void CopyBlock(uint[] dst, uint[] src)
        {
            dst[0] = src[0];
            dst[1] = src[1];
            dst[2] = src[2];
            dst[3] = src[3];
        }

        private static uint RotateLeft(uint value, int shift)
        {
            shift &= 31;
            return (value << shift) | (value >> (32 - shift));
        }

        private static uint RotateRight(uint value, int shift)
        {
            shift &= 31;
            return (value >> shift) | (value << (32 - shift));
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

            if (inputOffset < 0 || inputOffset + 16 > input.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputOffset));
            }

            if (outputOffset < 0 || outputOffset + 16 > output.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(outputOffset));
            }
        }

        private static uint LoadLittleEndian(byte[] data, int offset)
        {
            return data[offset]
                | ((uint)data[offset + 1] << 8)
                | ((uint)data[offset + 2] << 16)
                | ((uint)data[offset + 3] << 24);
        }

        private static void StoreLittleEndian(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }
    }
}
