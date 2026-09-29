using System;

namespace Metin2.Protocol.Security.Engines
{
    /// <summary>
    /// RC6-32/20/16 (Rivest et al., AES candidate): 128-bit block, 128-bit key,
    /// 20 rounds — matching `RC6` as selected by cipher.cpp:251-253
    /// (`RC6_Info`: block 16, key 16, rounds 20, cryptopp/rc6.h).
    ///
    /// Byte order: RC6 packs words LITTLE-endian (first byte = least significant
    /// byte of A; key bytes likewise — RC6 paper §2). Verified against the RC6
    /// paper appendix KATs (see Rc6EngineTests).
    /// Key length: 16, 24 or 32 bytes (CryptoPP VariableKeyLength&lt;16,16,32,8&gt;).
    /// No UnityEngine dependency.
    /// </summary>
    public class Rc6Engine : IBlockCipherEngine
    {
        public const int MinKeyLength = 16;
        public const int MaxKeyLength = 32;

        private const int Rounds = 20;
        private const int TableWords = 2 * Rounds + 4; // 44

        private const uint P32 = 0xB7E15163;
        private const uint Q32 = 0x9E3779B9;

        private readonly uint[] _s = new uint[TableWords];

        public int BlockSize => 16;

        public Rc6Engine(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if ((key.Length != 16 && key.Length != 24 && key.Length != 32))
            {
                throw new ArgumentException(
                    $"RC6 key must be 16, 24 or 32 bytes, got {key.Length}.",
                    nameof(key));
            }

            // Key schedule: L[] loaded little-endian, c = key.Length / 4 words.
            int c = key.Length / 4;
            uint[] l = new uint[c];
            for (int i = 0; i < c; i++)
            {
                l[i] = LoadLittleEndian(key, i * 4);
            }

            _s[0] = P32;
            for (int i = 1; i < TableWords; i++)
            {
                _s[i] = _s[i - 1] + Q32;
            }

            uint a = 0;
            uint b = 0;
            int v = 3 * Math.Max(c, TableWords);
            int ii = 0;
            int jj = 0;
            for (int s = 0; s < v; s++)
            {
                a = _s[ii] = RotateLeft(_s[ii] + a + b, 3);
                b = l[jj] = RotateLeft(l[jj] + a + b, (int)(a + b));
                ii = (ii + 1) % TableWords;
                jj = (jj + 1) % c;
            }
        }

        public void EncryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            uint a = LoadLittleEndian(input, inputOffset);
            uint b = LoadLittleEndian(input, inputOffset + 4);
            uint c = LoadLittleEndian(input, inputOffset + 8);
            uint d = LoadLittleEndian(input, inputOffset + 12);

            b += _s[0];
            d += _s[1];
            for (int i = 1; i <= Rounds; i++)
            {
                uint t = RotateLeft(b * (2 * b + 1), 5);
                uint u = RotateLeft(d * (2 * d + 1), 5);
                a = RotateLeft(a ^ t, (int)u) + _s[2 * i];
                c = RotateLeft(c ^ u, (int)t) + _s[2 * i + 1];
                uint temp = a;
                a = b;
                b = c;
                c = d;
                d = temp;
            }
            a += _s[2 * Rounds + 2];
            c += _s[2 * Rounds + 3];

            StoreLittleEndian(output, outputOffset, a);
            StoreLittleEndian(output, outputOffset + 4, b);
            StoreLittleEndian(output, outputOffset + 8, c);
            StoreLittleEndian(output, outputOffset + 12, d);
        }

        public void DecryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            uint a = LoadLittleEndian(input, inputOffset);
            uint b = LoadLittleEndian(input, inputOffset + 4);
            uint c = LoadLittleEndian(input, inputOffset + 8);
            uint d = LoadLittleEndian(input, inputOffset + 12);

            c -= _s[2 * Rounds + 3];
            a -= _s[2 * Rounds + 2];
            for (int i = Rounds; i >= 1; i--)
            {
                uint temp = d;
                d = c;
                c = b;
                b = a;
                a = temp;
                uint u = RotateLeft(d * (2 * d + 1), 5);
                uint t = RotateLeft(b * (2 * b + 1), 5);
                c = RotateRight(c - _s[2 * i + 1], (int)t) ^ u;
                a = RotateRight(a - _s[2 * i], (int)u) ^ t;
            }
            d -= _s[1];
            b -= _s[0];

            StoreLittleEndian(output, outputOffset, a);
            StoreLittleEndian(output, outputOffset + 4, b);
            StoreLittleEndian(output, outputOffset + 8, c);
            StoreLittleEndian(output, outputOffset + 12, d);
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
