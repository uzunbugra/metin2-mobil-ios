using System;

namespace Metin2.Protocol.Security.Engines
{
    /// <summary>
    /// SHACAL-2 (NESSIE submission, Lipmaa): 256-bit block, 128..512-bit key —
    /// matching `SHACAL2` as selected by cipher.cpp:288-290
    /// (`SHACAL2_Info`: block 32, `VariableKeyLength&lt;16, 16, 64&gt;`,
    /// cryptopp/shacal2.h).
    ///
    /// Literal port of CryptoPP shacal2.cpp (public domain): big-endian words,
    /// SHA-256 round function over 64 rounds with NO feedforward, key schedule =
    /// SHA-256 message expansion over the BE key words (short keys zero-padded
    /// to 64 bytes per GetUserKey, misc.h) fused with round-constant addition.
    /// K[64] are the SHA-256 round constants (FIPS 180-4 §4.2.2).
    /// Verified against CryptoPP TestVectors/shacal2.txt (see Shacal2EngineTests).
    /// No UnityEngine dependency.
    /// </summary>
    public class Shacal2Engine : IBlockCipherEngine
    {
        public const int MinKeyLength = 16;
        public const int MaxKeyLength = 64;

        public int BlockSize => 32;

        private readonly uint[] _rk = new uint[64];

        // The SHACAL-2 round constants are identical to the SHA-256 round constants
        // (CryptoPP shacal2.cpp; FIPS 180-4 §4.2.2).
        private static readonly uint[] K = new uint[64]
        {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5,
            0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
            0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc,
            0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7,
            0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
            0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3,
            0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5,
            0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
            0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
        };

        public Shacal2Engine(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length < MinKeyLength || key.Length > MaxKeyLength)
            {
                throw new ArgumentException(
                    $"SHACAL-2 key must be {MinKeyLength}..{MaxKeyLength} bytes, got {key.Length}.",
                    nameof(key));
            }

            // GetUserKey(BIG_ENDIAN_ORDER, rk, 64, key, keylen): key bytes copied
            // into a 64-byte zero buffer, loaded as 16 big-endian words.
            byte[] padded = new byte[64];
            System.Buffer.BlockCopy(key, 0, padded, 0, key.Length);
            for (int j = 0; j < 16; j++)
            {
                _rk[j] = LoadBigEndian(padded, j * 4);
            }

            // Fused message-schedule expansion + round-constant addition
            // (UncheckedSetKey: rk[16] first, then rk[0] += K[i]).
            for (int i = 0; i < 48; i++)
            {
                _rk[i + 16] = _rk[i] + s0(_rk[i + 1]) + _rk[i + 9] + s1(_rk[i + 14]);
                _rk[i] += K[i];
            }

            for (int i = 48; i < 64; i++)
            {
                _rk[i] += K[i];
            }
        }

        public void EncryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            uint a = LoadBigEndian(input, inputOffset);
            uint b = LoadBigEndian(input, inputOffset + 4);
            uint c = LoadBigEndian(input, inputOffset + 8);
            uint d = LoadBigEndian(input, inputOffset + 12);
            uint e = LoadBigEndian(input, inputOffset + 16);
            uint f = LoadBigEndian(input, inputOffset + 20);
            uint g = LoadBigEndian(input, inputOffset + 24);
            uint h = LoadBigEndian(input, inputOffset + 28);

            int ki = 0;
            for (int j = 0; j < 64; j += 8)
            {
                Round(ref a, ref b, ref c, ref d, ref e, ref f, ref g, ref h, _rk[ki++]);
                Round(ref h, ref a, ref b, ref c, ref d, ref e, ref f, ref g, _rk[ki++]);
                Round(ref g, ref h, ref a, ref b, ref c, ref d, ref e, ref f, _rk[ki++]);
                Round(ref f, ref g, ref h, ref a, ref b, ref c, ref d, ref e, _rk[ki++]);
                Round(ref e, ref f, ref g, ref h, ref a, ref b, ref c, ref d, _rk[ki++]);
                Round(ref d, ref e, ref f, ref g, ref h, ref a, ref b, ref c, _rk[ki++]);
                Round(ref c, ref d, ref e, ref f, ref g, ref h, ref a, ref b, _rk[ki++]);
                Round(ref b, ref c, ref d, ref e, ref f, ref g, ref h, ref a, _rk[ki++]);
            }

            StoreBigEndian(output, outputOffset, a);
            StoreBigEndian(output, outputOffset + 4, b);
            StoreBigEndian(output, outputOffset + 8, c);
            StoreBigEndian(output, outputOffset + 12, d);
            StoreBigEndian(output, outputOffset + 16, e);
            StoreBigEndian(output, outputOffset + 20, f);
            StoreBigEndian(output, outputOffset + 24, g);
            StoreBigEndian(output, outputOffset + 28, h);
        }

        /// <summary>SHACAL-2 decryption (KAT verification; CTR mode itself only uses encryption).</summary>
        public void DecryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            uint a = LoadBigEndian(input, inputOffset);
            uint b = LoadBigEndian(input, inputOffset + 4);
            uint c = LoadBigEndian(input, inputOffset + 8);
            uint d = LoadBigEndian(input, inputOffset + 12);
            uint e = LoadBigEndian(input, inputOffset + 16);
            uint f = LoadBigEndian(input, inputOffset + 20);
            uint g = LoadBigEndian(input, inputOffset + 24);
            uint h = LoadBigEndian(input, inputOffset + 28);

            int ki = 64;
            for (int j = 0; j < 64; j += 8)
            {
                InvRound(ref b, ref c, ref d, ref e, ref f, ref g, ref h, ref a, _rk[--ki]);
                InvRound(ref c, ref d, ref e, ref f, ref g, ref h, ref a, ref b, _rk[--ki]);
                InvRound(ref d, ref e, ref f, ref g, ref h, ref a, ref b, ref c, _rk[--ki]);
                InvRound(ref e, ref f, ref g, ref h, ref a, ref b, ref c, ref d, _rk[--ki]);
                InvRound(ref f, ref g, ref h, ref a, ref b, ref c, ref d, ref e, _rk[--ki]);
                InvRound(ref g, ref h, ref a, ref b, ref c, ref d, ref e, ref f, _rk[--ki]);
                InvRound(ref h, ref a, ref b, ref c, ref d, ref e, ref f, ref g, _rk[--ki]);
                InvRound(ref a, ref b, ref c, ref d, ref e, ref f, ref g, ref h, _rk[--ki]);
            }

            StoreBigEndian(output, outputOffset, a);
            StoreBigEndian(output, outputOffset + 4, b);
            StoreBigEndian(output, outputOffset + 8, c);
            StoreBigEndian(output, outputOffset + 12, d);
            StoreBigEndian(output, outputOffset + 16, e);
            StoreBigEndian(output, outputOffset + 20, f);
            StoreBigEndian(output, outputOffset + 24, g);
            StoreBigEndian(output, outputOffset + 28, h);
        }

        // SHA-256 round function (CryptoPP shacal2.cpp macro R).
        private static void Round(ref uint a, ref uint b, ref uint c, ref uint d,
            ref uint e, ref uint f, ref uint g, ref uint h, uint k)
        {
            h += S1(e) + Ch(e, f, g) + k;
            d += h;
            h += S0(a) + Maj(a, b, c);
        }

        // Inverse SHA-256 round function (CryptoPP shacal2.cpp macro P).
        private static void InvRound(ref uint a, ref uint b, ref uint c, ref uint d,
            ref uint e, ref uint f, ref uint g, ref uint h, uint k)
        {
            h -= S0(a) + Maj(a, b, c);
            d -= h;
            h -= S1(e) + Ch(e, f, g) + k;
        }

        private static uint S0(uint x)
        {
            return RotateRight(x, 2) ^ RotateRight(x, 13) ^ RotateRight(x, 22);
        }

        private static uint S1(uint x)
        {
            return RotateRight(x, 6) ^ RotateRight(x, 11) ^ RotateRight(x, 25);
        }

        private static uint s0(uint x)
        {
            return RotateRight(x, 7) ^ RotateRight(x, 18) ^ (x >> 3);
        }

        private static uint s1(uint x)
        {
            return RotateRight(x, 17) ^ RotateRight(x, 19) ^ (x >> 10);
        }

        private static uint Ch(uint x, uint y, uint z)
        {
            return z ^ (x & (y ^ z));
        }

        private static uint Maj(uint x, uint y, uint z)
        {
            return (x & y) | (z & (x | y));
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

            if (inputOffset < 0 || inputOffset + 32 > input.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputOffset));
            }

            if (outputOffset < 0 || outputOffset + 32 > output.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(outputOffset));
            }
        }

        private static uint LoadBigEndian(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24)
                | ((uint)data[offset + 1] << 16)
                | ((uint)data[offset + 2] << 8)
                | data[offset + 3];
        }

        private static void StoreBigEndian(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }
    }
}
