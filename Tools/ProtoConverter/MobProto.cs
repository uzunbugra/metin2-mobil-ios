using System;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Metin2.Tools.ProtoConverter
{
    /// <summary>
    /// mob_proto parser — MMPT format (DumpProto dump_proto.cpp:560-599
    /// SaveMobProto; client parity: PythonNonPlayer.cpp:7-70):
    ///   'MMPT' + elementCount + dataSize + MCOZ blob(dwRealSize = count × 255)
    ///
    /// TMobTable (255 B, pack(1), PythonNonPlayer.h:57-114 — field offsets
    /// computed; struct size verified empirically: locale_tr mob_proto blob
    /// = 343,485 B = 1,347 × 255, first record vnum 101 "Yabani Köpek"):
    ///   dwVnum@0, szName[25]@4, szLocaleName[25]@29, bType@54, bRank@55,
    ///   bBattleType@56, bLevel@57, bSize@58, dwGoldMin@59, dwGoldMax@63,
    ///   dwExp@67, dwMaxHP@71, bRegenCycle@75, bRegenPercent@76, wDef@77,
    ///   dwAIFlag@79, dwRaceFlag@83, dwImmuneFlag@87, bStr@91, bDex@92,
    ///   bCon@93, bInt@94, dwDamageRange[2]@95, sAttackSpeed@103,
    ///   sMovingSpeed@105, bAggresiveHPPct@107, wAggressiveSight@108,
    ///   wAttackRange@110, cEnchants[6]@112, cResists[11]@118,
    ///   dwResurrectionVnum@129, dwDropItemVnum@133, bMountCapacity@137,
    ///   bOnClickType@138, bEmpire@139, szFolder[65]@140, fDamMultiply@205,
    ///   dwSummonVnum@209, dwDrainSP@213, dwMobColor@217,
    ///   dwPolymorphItemVnum@221, Skills[5]{dwVnum,bLevel}@225 (5 B each),
    ///   bBerserkPoint@250, bStoneSkinPoint@251, bGodSpeedPoint@252,
    ///   bDeathBlowPoint@253, bRevivePoint@254.
    /// </summary>
    public static class MobProto
    {
        public const uint FourCC = 0x54504D4D; // MAKEFOURCC('M','M','P','T')
        public const int StructSize = 255;

        public static void Convert(string inputFile, string outputFile)
        {
            byte[] file = File.ReadAllBytes(inputFile);
            if (ProtoContainer.ReadFourCC(file) != FourCC)
            {
                throw new InvalidDataException($"Not a mob_proto (fourcc mismatch): {inputFile}");
            }

            int elementCount = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(4));
            byte[] table = ProtoContainer.DecompressBlob(file, ProtoContainer.MobProtoKey);

            if (table.Length % StructSize != 0)
            {
                throw new InvalidDataException(
                    $"mob_proto blob size {table.Length} not a multiple of TMobTable({StructSize})");
            }

            int recordCount = table.Length / StructSize;
            if (elementCount != recordCount)
            {
                throw new InvalidDataException(
                    $"mob_proto count mismatch: header {elementCount}, data {recordCount}");
            }

            var mobs = new object[recordCount];
            for (int i = 0; i < recordCount; i++)
            {
                ReadOnlySpan<byte> r = table.AsSpan(i * StructSize, StructSize);
                var skills = new object[5];
                for (int s = 0; s < 5; s++)
                {
                    skills[s] = new
                    {
                        vnum = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(225 + s * 5)),
                        level = r[225 + s * 5 + 4],
                    };
                }

                mobs[i] = new
                {
                    vnum = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(0)),
                    name = Cp1254.ReadFixedString(r.Slice(4, 25)),
                    localeName = Cp1254.ReadFixedString(r.Slice(29, 25)),
                    type = r[54],
                    rank = r[55],
                    battleType = r[56],
                    level = r[57],
                    size = r[58],
                    goldMin = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(59)),
                    goldMax = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(63)),
                    exp = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(67)),
                    maxHp = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(71)),
                    regenCycle = r[75],
                    regenPercent = r[76],
                    def = BinaryPrimitives.ReadUInt16LittleEndian(r.Slice(77)),
                    aiFlag = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(79)),
                    raceFlag = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(83)),
                    immuneFlag = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(87)),
                    str = r[91],
                    dex = r[92],
                    con = r[93],
                    int_ = r[94],
                    damageMin = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(95)),
                    damageMax = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(99)),
                    attackSpeed = BinaryPrimitives.ReadInt16LittleEndian(r.Slice(103)),
                    movingSpeed = BinaryPrimitives.ReadInt16LittleEndian(r.Slice(105)),
                    aggresiveHPPct = r[107],
                    aggressiveSight = BinaryPrimitives.ReadUInt16LittleEndian(r.Slice(108)),
                    attackRange = BinaryPrimitives.ReadUInt16LittleEndian(r.Slice(110)),
                    resurrectionVnum = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(129)),
                    dropItemVnum = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(133)),
                    mountCapacity = r[137],
                    onClickType = r[138],
                    empire = r[139],
                    folder = Cp1254.ReadFixedString(r.Slice(140, 65)),
                    damMultiply = BitConverter.ToSingle(r.Slice(205)),
                    summonVnum = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(209)),
                    drainSP = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(213)),
                    mobColor = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(217)),
                    polymorphItemVnum = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(221)),
                    skills,
                    berserkPoint = r[250],
                    stoneSkinPoint = r[251],
                    godSpeedPoint = r[252],
                    deathBlowPoint = r[253],
                    revivePoint = r[254],
                };
            }

            var doc = new
            {
                format = "MMPT",
                source = inputFile,
                sourceSha256 = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file)).ToLowerInvariant(),
                structSize = StructSize,
                count = recordCount,
                mobs,
            };

            string json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            File.WriteAllText(outputFile, json);
            Console.WriteLine($"mob_proto: {recordCount} mobs -> {outputFile} ({json.Length} B JSON)");
        }
    }
}
