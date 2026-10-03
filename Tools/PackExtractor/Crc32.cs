using System;
using System.IO;

namespace Metin2.Tools.PackExtractor
{
    /// <summary>
    /// CRC-32 (IEEE 802.3, reflected, poly 0xEDB88320, init/xor 0xFFFFFFFF) —
    /// parity with the PC client's EterBase/CRC32.cpp GetCRC32
    /// (standard zlib table; verified against the table in the source).
    /// </summary>
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }

                table[i] = crc;
            }

            return table;
        }

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFF;
        }
    }
}
