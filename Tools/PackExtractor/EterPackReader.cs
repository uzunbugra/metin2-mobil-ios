using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Metin2.Tools.PackExtractor
{
    /// <summary>
    /// EterPack .eix/.epk reader — port of EterPack/EterPack.cpp
    /// (__BuildIndex:407-465, Get/ReadData dispatch:528-628) and
    /// EterPack.h struct layout.
    ///
    /// EIX layout:
    ///   'EPKD' + version(=2) + indexCount + TEterPackIndex[indexCount]
    ///   — or the same content wrapped in an MCOZ blob encrypted with the
    ///     EterPack index key (fourcc == 'MCOZ' → m_bEncrypted).
    ///
    /// TEterPackIndex (192 B, #pragma pack(4), EterPack.h:47-62) — field
    /// offsets verified empirically against locale_tr.eix (index size
    /// 46476 = 12 header + 242 entries × 192; char[161] ends unaligned so
    /// filename_crc pads to 168):
    ///   long id @0; char filename[161] @4; (+3 pad)
    ///   DWORD filename_crc @168; long real_data_size @172; long data_size @176;
    ///   DWORD data_crc @180; long data_position @184; char compressed_type @188;
    ///   (+3 pad)
    ///
    /// File data in the .epk at data_position/data_size:
    ///   type 0 NONE: raw; type 1 COMPRESS: plain MCOZ (LZO only);
    ///   type 2 SECURITY: MCOZ + TEA (security key) + CRC32 over the stored
    ///   bytes (EterPack.cpp:528-548); type 3/4/5 PANAMA/HYBRIDCRYPT: not
    ///   supported (keys come from the server handshake).
    /// </summary>
    public sealed class EterPackReader
    {
        public const uint PackCC = 0x44504B45;      // MAKEFOURCC('E','P','K','D')
        public const uint Version = 2;
        public const int IndexEntrySize = 192;
        public const int IndexHeaderSize = 12;      // fourcc + version + count

        /// <summary>s_adwEterPackKey (EterPack.cpp:325-331) — EIX encryption.</summary>
        public static readonly uint[] IndexKey = { 45129401, 92367215, 681285731, 1710201 };

        /// <summary>s_adwEterPackSecurityKey (EterPack.cpp:333-339) — type-2 file data.</summary>
        public static readonly uint[] SecurityKey = { 78952482, 527348324, 1632942, 486274726 };

        public sealed class PackEntry
        {
            public required string FileName { get; init; }
            public required int Id { get; init; }
            public required uint FileNameCrc { get; init; }
            public required int RealDataSize { get; init; }
            public required int DataSize { get; init; }
            public required uint DataCrc { get; init; }
            public required long DataPosition { get; init; }
            public required byte CompressedType { get; init; }
        }

        public string IndexPath { get; }
        public bool IndexEncrypted { get; }
        public int IndexCount { get; }
        public IReadOnlyList<PackEntry> Entries { get; }

        private readonly byte[] _epkData;

        public EterPackReader(string eixPath, string epkPath)
        {
            IndexPath = eixPath;
            byte[] eix = File.ReadAllBytes(eixPath);
            _epkData = File.ReadAllBytes(epkPath);

            if (eix.Length < IndexHeaderSize)
            {
                throw new InvalidDataException($"EIX too short: {eixPath} ({eix.Length} B)");
            }

            uint fourcc = BinaryPrimitives.ReadUInt32LittleEndian(eix);
            byte[] index;
            if (fourcc == PackCC)
            {
                IndexEncrypted = false;
                index = eix;
            }
            else if (fourcc == Mcoz.FourCC)
            {
                IndexEncrypted = true;
                index = Mcoz.Decompress(eix, IndexKey);
            }
            else
            {
                throw new InvalidDataException(
                    $"EIX fourcc error: 0x{fourcc:X8} (expected EPKD or MCOZ) — {eixPath}");
            }

            if (index.Length < IndexHeaderSize)
            {
                throw new InvalidDataException("Decompressed index too short for header");
            }

            uint version = BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(4));
            if (version != Version)
            {
                throw new InvalidDataException($"EIX version error: {version} (expected {Version})");
            }

            int indexCount = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(8));
            if (indexCount < 0 ||
                index.Length < IndexHeaderSize + (long)indexCount * IndexEntrySize)
            {
                throw new InvalidDataException(
                    $"EIX size error: indexCount {indexCount}, index {index.Length} B");
            }

            IndexCount = indexCount;

            var entries = new List<PackEntry>(indexCount);
            for (int i = 0; i < indexCount; i++)
            {
                ReadOnlySpan<byte> entry = index.AsSpan(
                    IndexHeaderSize + i * IndexEntrySize, IndexEntrySize);

                uint filenameCrc = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(168));
                if (filenameCrc == 0)
                {
                    // Free/deleted index slot (EterPack.cpp:471-474).
                    continue;
                }

                entries.Add(new PackEntry
                {
                    Id = BinaryPrimitives.ReadInt32LittleEndian(entry.Slice(0)),
                    FileName = ReadFixedString(entry.Slice(4, 161)),
                    FileNameCrc = filenameCrc,
                    RealDataSize = BinaryPrimitives.ReadInt32LittleEndian(entry.Slice(172)),
                    DataSize = BinaryPrimitives.ReadInt32LittleEndian(entry.Slice(176)),
                    DataCrc = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(180)),
                    DataPosition = BinaryPrimitives.ReadInt32LittleEndian(entry.Slice(184)),
                    CompressedType = entry[188],
                });
            }

            Entries = entries;
        }

        /// <summary>
        /// Reads and decodes a file's data. Fail-closed: bounds-checked,
        /// CRC-verified for SECURITY entries (client parity), unsupported
        /// compression types throw instead of guessing.
        /// </summary>
        public byte[] ReadEntry(PackEntry entry)
        {
            if (entry.DataPosition < 0 ||
                entry.DataSize < 0 ||
                entry.DataPosition + entry.DataSize > _epkData.Length)
            {
                throw new InvalidDataException(
                    $"Entry '{entry.FileName}' out of epk bounds: pos {entry.DataPosition}, size {entry.DataSize}");
            }

            ReadOnlySpan<byte> stored = _epkData.AsSpan(
                (int)entry.DataPosition, entry.DataSize);

            bool securityCheckRequired = entry.CompressedType is 2 or 3;
            if (securityCheckRequired)
            {
                uint crc = Crc32.Compute(stored);
                if (crc != entry.DataCrc)
                {
                    throw new InvalidDataException(
                        $"Entry '{entry.FileName}' CRC mismatch: {crc:X8} != {entry.DataCrc:X8}");
                }
            }

            return entry.CompressedType switch
            {
                0 => stored.ToArray(),
                1 => Mcoz.Decompress(stored, null),
                2 => Mcoz.Decompress(stored, SecurityKey),
                3 => throw new NotSupportedException(
                    $"'{entry.FileName}' uses PANAMA compression (server-handshake keys) — not supported"),
                4 or 5 => throw new NotSupportedException(
                    $"'{entry.FileName}' uses HYBRIDCRYPT compression (server-handshake keys) — not supported"),
                _ => throw new NotSupportedException(
                    $"'{entry.FileName}' has unknown compressed_type {entry.CompressedType}"),
            };
        }

        private static string ReadFixedString(ReadOnlySpan<byte> field)
        {
            int length = field.IndexOf((byte)0);
            if (length < 0)
            {
                length = field.Length;
            }

            return Encoding.ASCII.GetString(field.Slice(0, length));
        }
    }
}
