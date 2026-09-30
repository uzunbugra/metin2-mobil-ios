using System;

namespace Metin2.Protocol.Security.Engines
{
    /// <summary>
    /// RC5-32 (Rivest, 1994): 64-bit block, variable key length, variable rounds —
    /// matching `RC5` as selected by cipher.cpp:278-280
    /// (`RC5_Info`: block 8, `VariableKeyLength&lt;16, 0, 255&gt;`,
    /// `VariableRounds&lt;16&gt;`, cryptopp/rc5.h).
    ///
    /// CryptoPP default is 16 rounds; Rivest's published vectors are for 12 rounds,
    /// so this engine is round-parametric (default 16, KATs use 12).
    /// Words pack LITTLE-endian (first byte = least significant byte of A;
    /// key bytes likewise — RFC 2040 §6.1/§6.3). Key schedule, P/Q constants
    /// and round function follow RFC 2040 §5–6 exactly.
    /// Verified against Rivest paper KATs (see Rc5EngineTests).
    /// No UnityEngine dependency.
    /// </summary>
    public class Rc5Engine : IBlockCipherEngine
    {
        public const int KeyLength = 16;

        /// <summary>CryptoPP default rounds (`VariableRounds&lt;16&gt;`, rc5.h).</summary>
        public const int DefaultRounds = 16;

        private const uint P32 = 0xB7E15163;
        private const uint Q32 = 0x9E3779B9;

        private readonly int _rounds;
        private readonly uint[] _s;

        public int BlockSize => 8;

        /// <summary>Round count actually used (12 for published KATs, 16 on the wire).</summary>
        public int Rounds => _rounds;

        public Rc5Engine(byte[] key)
            : this(key, DefaultRounds)
        {
        }

        public Rc5Engine(byte[] key, int rounds)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length != KeyLength)
            {
                throw new ArgumentException(
                    $"RC5 key must be {KeyLength} bytes, got {key.Length}.",
                    nameof(key));
            }

            if (rounds < 0 || rounds > 255)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rounds), "RC5 rounds must be 0..255.");
            }

            _rounds = rounds;
            int t = 2 * (rounds + 1);
            _s = new uint[t];

            // L[] loaded little-endian (RFC 2040 §5.3: K[i] << 8*(i%4)).
            int c = key.Length / 4;
            uint[] l = new uint[c];
            for (int i = key.Length - 1; i >= 0; i--)
            {
                l[i / 4] = (l[i / 4] << 8) + key[i];
            }

            _s[0] = P32;
            for (int i = 1; i < t; i++)
            {
                _s[i] = _s[i - 1] + Q32;
            }

            uint a = 0;
            uint b = 0;
            int n = 3 * Math.Max(t, c);
            int ii = 0;
            int jj = 0;
            for (int k = 0; k < n; k++)
            {
                a = _s[ii] = RotateLeft(_s[ii] + a + b, 3);
                b = l[jj] = RotateLeft(l[jj] + a + b, (int)(a + b));
                ii = (ii + 1) % t;
                jj = (jj + 1) % c;
            }
        }

        public void EncryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            uint a = LoadLittleEndian(input, inputOffset) + _s[0];
            uint b = LoadLittleEndian(input, inputOffset + 4) + _s[1];
            for (int i = 1; i <= _rounds; i++)
            {
                a = RotateLeft(a ^ b, (int)b) + _s[2 * i];
                b = RotateLeft(b ^ a, (int)a) + _s[2 * i + 1];
            }

            StoreLittleEndian(output, outputOffset, a);
            StoreLittleEndian(output, outputOffset + 4, b);
        }

        /// <summary>RC5 decryption (KAT verification; CTR mode itself only uses encryption).</summary>
        public void DecryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            uint a = LoadLittleEndian(input, inputOffset);
            uint b = LoadLittleEndian(input, inputOffset + 4);
            for (int i = _rounds; i >= 1; i--)
            {
                b = RotateRight(b - _s[2 * i + 1], (int)a) ^ a;
                a = RotateRight(a - _s[2 * i], (int)b) ^ b;
            }
            b -= _s[1];
            a -= _s[0];

            StoreLittleEndian(output, outputOffset, a);
            StoreLittleEndian(output, outputOffset + 4, b);
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
