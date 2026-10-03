using System;
using System.Buffers.Binary;
using System.Text;
using Metin2.Tools.PackExtractor;

namespace Metin2.Tools.ProtoConverter
{
    /// <summary>
    /// CP1254 (Türkçe Windows codepage — fullbinary/locale.cfg: "1254")
    /// decoding without the CodePagesEncodingProvider dependency: CP1254 is
    /// Latin-1 with five Turkish overrides (D0 Ğ, DD İ, DE Ş, FD ı, FE ş).
    /// </summary>
    public static class Cp1254
    {
        private static readonly char[] Table = BuildTable();

        private static char[] BuildTable()
        {
            var table = new char[256];
            for (int i = 0x20; i <= 0xFF; i++)
            {
                table[i] = (char)i; // Latin-1 base
            }

            table[0xD0] = 'Ğ';
            table[0xDD] = 'İ';
            table[0xDE] = 'Ş';
            table[0xFD] = 'ı';
            table[0xFE] = 'ş';
            return table;
        }

        /// <summary>Decodes a fixed-size NUL-padded string field.</summary>
        public static string ReadFixedString(ReadOnlySpan<byte> field)
        {
            int length = field.IndexOf((byte)0);
            if (length < 0)
            {
                length = field.Length;
            }

            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = Table[field[i]];
            }

            return new string(chars);
        }
    }

    /// <summary>
    /// Common proto container handling — both item_proto and mob_proto are
    /// [own header] + MCOZ blob (LZO1X + TEA) keyed with their proto keys
    /// (DumpProto dump_proto.cpp:551-557, 989-995; client parity:
    /// ItemManager.cpp:255-294, PythonNonPlayer.cpp:9-15).
    /// </summary>
    public static class ProtoContainer
    {
        public static readonly uint[] MobProtoKey = { 4813894, 18955, 552631, 6822045 };
        public static readonly uint[] ItemProtoKey = { 173217, 72619434, 408587239, 27973291 };

        /// <summary>Finds the MCOZ blob inside a proto file and returns the
        /// decompressed struct array bytes.</summary>
        public static byte[] DecompressBlob(byte[] file, uint[] key)
        {
            ReadOnlySpan<byte> span = file;
            if (span.Length < 4 || BitConverter.ToUInt32(span.Slice(0, 4)) == Mcoz.FourCC)
            {
                return Mcoz.Decompress(span, key);
            }

            for (int i = 4; i <= 64 && i + 4 <= span.Length; i++)
            {
                if (BitConverter.ToUInt32(span.Slice(i, 4)) == Mcoz.FourCC)
                {
                    return Mcoz.Decompress(span.Slice(i), key);
                }
            }

            throw new InvalidDataException("No MCOZ blob found in proto file");
        }

        /// <summary>FourCC check helper.</summary>
        public static uint ReadFourCC(byte[] file) =>
            BinaryPrimitives.ReadUInt32LittleEndian(file);
    }
}
