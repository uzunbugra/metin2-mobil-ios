using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Metin2.Tools.ProtoConverter
{
    /// <summary>
    /// item_proto parser — MIPX format (DumpProto dump_proto.cpp:997-1059
    /// SaveItemProto; client parity: ItemManager.cpp:255-294):
    ///   'MIPX' + version(1) + stride(sizeof(TClientItemTable)) +
    ///   elementCount + dataSize + MCOZ blob
    ///
    /// TClientItemTable (156 B, pack(1), dump_proto.cpp:173-203 — offsets
    /// computed; stride verified empirically: locale_tr item_proto header
    /// stride = 0x9C = 156, count = 5,929):
    ///   dwVnum@0, dwVnumRange@4, szName[25]@8, szLocaleName[25]@33,
    ///   bType@58, bSubType@59, bWeight@60, bSize@61, dwAntiFlags@62,
    ///   dwFlags@66, dwWearFlags@70, dwImmuneFlag@74, dwGold@78,
    ///   dwShopBuyPrice@82, aLimits[2]{bType,lValue}@86 (5 B each),
    ///   aApplies[3]{bType,lValue}@96 (5 B each), alValues[6]@111,
    ///   alSockets[3]@135, dwRefinedVnum@147, wRefineSet@151,
    ///   bAlterToMagicItemPct@153, bSpecular@154, bGainSocketPct@155.
    ///
    /// Optional merges (same vnum key):
    ///   item_list.txt: "vnum \t type \t iconPath [\t modelPath]"
    ///   (ItemManager.cpp:108-173) → icon/model paths for the Unity import.
    ///   itemdesc.txt: "vnum \t name \t description" (CP1254).
    /// </summary>
    public static class ItemProto
    {
        public const uint FourCC = 0x5850494D; // MAKEFOURCC('M','I','P','X')
        public const uint Version = 1;
        public const int StructSize = 156;

        public static void Convert(
            string inputFile,
            string outputFile,
            string? itemListFile,
            string? itemDescFile)
        {
            byte[] file = File.ReadAllBytes(inputFile);
            if (ProtoContainer.ReadFourCC(file) != FourCC)
            {
                throw new InvalidDataException($"Not an item_proto (fourcc mismatch): {inputFile}");
            }

            uint version = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(4));
            if (version != Version)
            {
                throw new InvalidDataException($"item_proto version {version} (expected {Version})");
            }

            int stride = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(8));
            if (stride != StructSize)
            {
                throw new InvalidDataException(
                    $"item_proto stride {stride} != sizeof(TClientItemTable) {StructSize}");
            }

            int elementCount = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(12));
            byte[] table = ProtoContainer.DecompressBlob(file, ProtoContainer.ItemProtoKey);

            if (table.Length != (long)elementCount * StructSize)
            {
                throw new InvalidDataException(
                    $"item_proto blob size {table.Length} != count {elementCount} × {StructSize}");
            }

            var icons = itemListFile != null ? ReadItemList(itemListFile) : new Dictionary<uint, (string icon, string? model)>();
            var descs = itemDescFile != null ? ReadItemDesc(itemDescFile) : new Dictionary<uint, (string name, string desc)>();

            var items = new object[elementCount];
            for (int i = 0; i < elementCount; i++)
            {
                ReadOnlySpan<byte> r = table.AsSpan(i * StructSize, StructSize);

                var limits = new object[2];
                var applies = new object[3];
                for (int k = 0; k < 2; k++)
                {
                    limits[k] = new
                    {
                        type = r[86 + k * 5],
                        value = BinaryPrimitives.ReadInt32LittleEndian(r.Slice(86 + k * 5 + 1)),
                    };
                }

                for (int k = 0; k < 3; k++)
                {
                    applies[k] = new
                    {
                        type = r[96 + k * 5],
                        value = BinaryPrimitives.ReadInt32LittleEndian(r.Slice(96 + k * 5 + 1)),
                    };
                }

                var values = new int[6];
                for (int k = 0; k < 6; k++)
                {
                    values[k] = BinaryPrimitives.ReadInt32LittleEndian(r.Slice(111 + k * 4));
                }

                var sockets = new int[3];
                for (int k = 0; k < 3; k++)
                {
                    sockets[k] = BinaryPrimitives.ReadInt32LittleEndian(r.Slice(135 + k * 4));
                }

                uint vnum = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(0));
                icons.TryGetValue(vnum, out (string icon, string? model) list);
                descs.TryGetValue(vnum, out (string name, string desc) descEntry);

                items[i] = new
                {
                    vnum,
                    vnumRange = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(4)),
                    name = Cp1254.ReadFixedString(r.Slice(8, 25)),
                    localeName = Cp1254.ReadFixedString(r.Slice(33, 25)),
                    type = r[58],
                    subType = r[59],
                    weight = r[60],
                    size = r[61],
                    antiFlags = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(62)),
                    flags = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(66)),
                    wearFlags = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(70)),
                    immuneFlag = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(74)),
                    gold = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(78)),
                    shopBuyPrice = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(82)),
                    limits,
                    applies,
                    values,
                    sockets,
                    refinedVnum = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(147)),
                    refineSet = BinaryPrimitives.ReadUInt16LittleEndian(r.Slice(151)),
                    alterToMagicItemPct = r[153],
                    specular = r[154],
                    gainSocketPct = r[155],
                    iconPath = list.icon ?? null,
                    modelPath = list.model,
                    descName = descEntry.name,
                    description = descEntry.desc,
                };
            }

            var doc = new
            {
                format = "MIPX",
                source = inputFile,
                sourceSha256 = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file)).ToLowerInvariant(),
                structSize = StructSize,
                count = elementCount,
                mergedItemList = itemListFile,
                mergedItemDesc = itemDescFile,
                items,
            };

            string json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            File.WriteAllText(outputFile, json);
            Console.WriteLine(
                $"item_proto: {elementCount} items (icons: {icons.Count}, descs: {descs.Count}) -> {outputFile} ({json.Length} B JSON)");
        }

        /// <summary>item_list.txt: "vnum \t type \t iconPath [\t modelPath]" (CP1254).</summary>
        private static Dictionary<uint, (string icon, string? model)> ReadItemList(string path)
        {
            var map = new Dictionary<uint, (string, string?)>();
            foreach (string line in File.ReadAllLines(path, Encoding.Latin1))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split('\t');
                if (parts.Length < 3 || !uint.TryParse(parts[0].Trim(), out uint vnum))
                {
                    continue;
                }

                string icon = parts[2].Trim();
                string? model = parts.Length >= 4 && parts[3].Trim().Length > 0 ? parts[3].Trim() : null;
                map[vnum] = (icon, model);
            }

            return map;
        }

        /// <summary>itemdesc.txt: "vnum \t name \t description" (CP1254).</summary>
        private static Dictionary<uint, (string name, string desc)> ReadItemDesc(string path)
        {
            var map = new Dictionary<uint, (string, string)>();
            foreach (string rawLine in File.ReadAllLines(path, Encoding.Latin1))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                // Description text may itself contain tabs — split max 3.
                string[] parts = rawLine.Split('\t', 3);
                if (parts.Length < 3 || !uint.TryParse(parts[0].Trim(), out uint vnum))
                {
                    continue;
                }

                map[vnum] = (DecodeCp1254(parts[1].Trim()), DecodeCp1254(parts[2].Trim()));
            }

            return map;
        }

        private static string DecodeCp1254(string latin1)
        {
            byte[] bytes = Encoding.Latin1.GetBytes(latin1);
            return Cp1254.ReadFixedString(bytes);
        }
    }
}
