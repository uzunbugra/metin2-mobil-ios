using System;

namespace Metin2.Tools.PackExtractor
{
    /// <summary>
    /// TEA (Wheeler-Needham) block codec over little-endian DWORD streams —
    /// birebir port of the PC client's EterBase/tea.cpp (tea_code/tea_decode,
    /// 32 rounds, DELTA 0x9E3779B9). Used by the MCOZ blob wrapper
    /// (EterBase/lzo.cpp tea_encrypt/tea_decrypt).
    ///
    /// Source parity notes:
    /// - Block order: tea_code(sz = src[1], sy = src[0]) → y = src[0], z = src[1].
    /// - Decrypt pads size up to the next multiple of 8 (tea.cpp:91-94).
    /// - C operator precedence "(z &lt;&lt; 4 ^ z >> 5)" == "((z&lt;&lt;4) ^ (z>>5))" holds in C#.
    /// </summary>
    public static class Tea
    {
        private const uint Delta = 0x9E3779B9;
        private const int Rounds = 32;

        /// <summary>
        /// Decrypts <paramref name="size"/> bytes (padded up to a multiple of
        /// 8, mirroring tea_decrypt) from <paramref name="src"/> with the
        /// 4-DWORD key. Returns the padded-length plaintext buffer.
        /// </summary>
        public static byte[] Decrypt(ReadOnlySpan<byte> src, uint[] key, int size)
        {
            if (key.Length != 4)
            {
                throw new ArgumentException("TEA key must be 4 DWORDs", nameof(key));
            }

            int resize = size % 8 != 0 ? size + 8 - (size % 8) : size;
            if (src.Length < resize)
            {
                throw new InvalidDataException($"TEA source too short: {src.Length} < {resize}");
            }

            var dst = new byte[resize];
            for (int block = 0; block < resize >> 3; block++)
            {
                uint y = BitConverter.ToUInt32(src.Slice(block * 8, 4));
                uint z = BitConverter.ToUInt32(src.Slice(block * 8 + 4, 4));

                // tea.cpp:51 uses "#pragma warning(disable:4307)" for this
                // deliberate wraparound (DELTA * 32 rounds).
                uint sum = unchecked(Delta * Rounds);
                for (int n = 0; n < Rounds; n++)
                {
                    z -= ((y << 4 ^ y >> 5) + y) ^ (sum + key[(sum >> 11) & 3]);
                    sum -= Delta;
                    y -= ((z << 4 ^ z >> 5) + z) ^ (sum + key[sum & 3]);
                }

                BitConverter.GetBytes(y).CopyTo(dst, block * 8);
                BitConverter.GetBytes(z).CopyTo(dst, block * 8 + 4);
            }

            return dst;
        }
    }
}
