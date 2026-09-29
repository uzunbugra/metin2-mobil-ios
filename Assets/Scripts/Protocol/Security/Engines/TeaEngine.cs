using System;

namespace Metin2.Protocol.Security.Engines
{
    /// <summary>
    /// Original TEA (Wheeler–Needham, 1994), NOT XTEA: 64-bit block, 128-bit key,
    /// 32 cycles — matching `TEA` as selected by cipher.cpp:284-286
    /// (`TEA_Info`: block 8, key 16, rounds 32, cryptopp/tea.h:16).
    ///
    /// Verified against published KATs (see TeaEngineTests). Word packing is
    /// big-endian per all published vectors; byte order vs CryptoPP tea.cpp is
    /// UNVERIFIED (tea.cpp not vendored) — confirm with live traffic.
    /// No UnityEngine dependency.
    /// </summary>
    public class TeaEngine : IBlockCipherEngine
    {
        public const int KeyLength = 16;

        private const uint Delta = 0x9E3779B9;
        private const int Rounds = 32;

        private readonly uint _k0;
        private readonly uint _k1;
        private readonly uint _k2;
        private readonly uint _k3;

        public int BlockSize => 8;

        public TeaEngine(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length != KeyLength)
            {
                throw new ArgumentException(
                    $"TEA key must be {KeyLength} bytes, got {key.Length}.",
                    nameof(key));
            }

            _k0 = LoadBigEndian(key, 0);
            _k1 = LoadBigEndian(key, 4);
            _k2 = LoadBigEndian(key, 8);
            _k3 = LoadBigEndian(key, 12);
        }

        public void EncryptBlock(byte[] counter, int counterOffset, byte[] keystream, int keystreamOffset)
        {
            CheckArgs(counter, counterOffset, keystream, keystreamOffset);

            uint v0 = LoadBigEndian(counter, counterOffset);
            uint v1 = LoadBigEndian(counter, counterOffset + 4);
            uint sum = 0;

            for (int i = 0; i < Rounds; i++)
            {
                sum += Delta;
                v0 += ((v1 << 4) + _k0) ^ (v1 + sum) ^ ((v1 >> 5) + _k1);
                v1 += ((v0 << 4) + _k2) ^ (v0 + sum) ^ ((v0 >> 5) + _k3);
            }

            StoreBigEndian(keystream, keystreamOffset, v0);
            StoreBigEndian(keystream, keystreamOffset + 4, v1);
        }

        /// <summary>TEA decryption (KAT verification; CTR mode itself only uses encryption).</summary>
        public void DecryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            CheckArgs(input, inputOffset, output, outputOffset);

            uint v0 = LoadBigEndian(input, inputOffset);
            uint v1 = LoadBigEndian(input, inputOffset + 4);
            uint sum = Delta << 5; // 0xC6EF3720

            for (int i = 0; i < Rounds; i++)
            {
                v1 -= ((v0 << 4) + _k2) ^ (v0 + sum) ^ ((v0 >> 5) + _k3);
                v0 -= ((v1 << 4) + _k0) ^ (v1 + sum) ^ ((v1 >> 5) + _k1);
                sum -= Delta;
            }

            StoreBigEndian(output, outputOffset, v0);
            StoreBigEndian(output, outputOffset + 4, v1);
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
