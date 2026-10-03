using System;
using System.IO;

namespace Metin2.Tools.PackExtractor
{
    /// <summary>
    /// MCOZ compressed-blob wrapper — port of EterBase/lzo.cpp
    /// (CLZObject::Compress/Decompress/Encrypt/__Decrypt).
    ///
    /// On-disk layout:
    ///   THeader (16 B): dwFourCC 'MCOZ', dwEncryptSize, dwCompressedSize,
    ///                   dwRealSize
    ///   body (dwEncryptSize bytes when encrypted):
    ///     [inner 'MCOZ' 4 B][LZO1X data (dwCompressedSize)][padding to 8]
    ///
    /// Decompress parity (lzo.cpp:261-305):
    ///   - encrypted: tea_decrypt over [inner fourcc .. +dwEncryptSize);
    ///     first decrypted DWORD must be 'MCOZ' (key verification,
    ///     lzo.cpp:274-278); LZO input starts at +4.
    ///   - plain: LZO input at +4 (m_pbIn = pvIn + 16 + 4, lzo.cpp:195).
    ///   - produced size must equal dwRealSize (lzo.cpp:298-302).
    /// </summary>
    public static class Mcoz
    {
        public const uint FourCC = 0x5A4F434D; // MAKEFOURCC('M','C','O','Z')

        public static byte[] Decompress(ReadOnlySpan<byte> blob, uint[]? key)
        {
            if (blob.Length < 16)
            {
                throw new InvalidDataException("MCOZ blob too short for header");
            }

            uint fourcc = BitConverter.ToUInt32(blob.Slice(0, 4));
            if (fourcc != FourCC)
            {
                throw new InvalidDataException($"Not an MCOZ blob (fourcc 0x{fourcc:X8})");
            }

            uint encryptSize = BitConverter.ToUInt32(blob.Slice(4, 4));
            uint compressedSize = BitConverter.ToUInt32(blob.Slice(8, 4));
            uint realSize = BitConverter.ToUInt32(blob.Slice(12, 4));

            if (realSize > 512 * 1024 * 1024)
            {
                throw new InvalidDataException($"MCOZ real size implausible: {realSize}");
            }

            ReadOnlySpan<byte> body = blob.Slice(16);

            if (encryptSize != 0)
            {
                if (key == null || key.Length != 4)
                {
                    throw new InvalidDataException("Encrypted MCOZ blob but no TEA key provided");
                }

                if (body.Length < encryptSize)
                {
                    throw new InvalidDataException($"MCOZ body short: {body.Length} < encryptSize {encryptSize}");
                }

                byte[] decrypted = Tea.Decrypt(body.Slice(0, (int)encryptSize), key, (int)encryptSize);

                // Key verification: the inner fourcc must decrypt to MCOZ.
                uint inner = BitConverter.ToUInt32(decrypted, 0);
                if (inner != FourCC)
                {
                    throw new InvalidDataException("MCOZ key incorrect (inner fourcc mismatch)");
                }

                if (decrypted.Length < 4 + compressedSize)
                {
                    throw new InvalidDataException("MCOZ decrypted body shorter than compressed size");
                }

                return Lzo1x.Decompress(
                    decrypted.AsSpan(4, (int)compressedSize),
                    (int)realSize);
            }

            // Plain path: inner fourcc present but unchecked by the client
            // (lzo.cpp:286-296 else branch); we validate it as a sanity check.
            if (body.Length < 4 + compressedSize)
            {
                throw new InvalidDataException("MCOZ body shorter than compressed size");
            }

            uint plainInner = BitConverter.ToUInt32(body.Slice(0, 4));
            if (plainInner != FourCC)
            {
                throw new InvalidDataException($"MCOZ plain inner fourcc mismatch (0x{plainInner:X8})");
            }

            return Lzo1x.Decompress(
                body.Slice(4, (int)compressedSize),
                (int)realSize);
        }
    }
}
