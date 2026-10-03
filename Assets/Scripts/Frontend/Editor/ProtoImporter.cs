#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metin2.Frontend.Proto;
using UnityEditor;
using UnityEngine;

namespace Metin2.Frontend.EditorTools
{
    /// <summary>
    /// SP10-2/SP10-4 Unity side: imports the ProtoConverter JSON output
    /// (Extracted/locale_tr) into ScriptableObject databases under
    /// Assets/Resources/GameData and converts the extracted icon pack TGA
    /// files into sprite assets with mobile-friendly import settings.
    ///
    /// Menu: Metin2 → Import Proto Data
    /// Headless:
    ///   Unity -batchmode -quit -projectPath &lt;repo&gt;
    ///     -executeMethod Metin2.Frontend.EditorTools.ProtoImporter.ImportAll
    ///
    /// Pipeline (all outputs are local-only per ADR-0003 — they embed
    /// copyrighted name strings and icon art):
    ///   PackExtractor extract locale_tr → ProtoConverter mob/item →
    ///   ProtoImporter.ImportAll → Assets/Resources/GameData/
    ///     ├── ItemDatabase.asset (+ per-item icon sprite links)
    ///     ├── MobDatabase.asset
    ///     └── Icons/**.tga (sprite, ASTC 6x6 on iOS)
    /// </summary>
    public static class ProtoImporter
    {
        // The project root IS the repo root (iOS line, SPRINT_07 restructure).
        private const string RepoRoot = ".";
        private const string ItemJsonPath = "Extracted/locale_tr/item_proto.json";
        private const string MobJsonPath = "Extracted/locale_tr/mob_proto.json";
        private const string IconSourceRoot = "Extracted/icon";
        private const string GameDataFolder = "Assets/Resources/GameData";
        private const string IconDestRoot = GameDataFolder + "/Icons";

        [MenuItem("Metin2/Import Proto Data")]
        public static void ImportAll()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), RepoRoot));
            string itemJson = Path.Combine(repoRoot, ItemJsonPath);
            string mobJson = Path.Combine(repoRoot, MobJsonPath);
            string iconRoot = Path.Combine(repoRoot, IconSourceRoot);

            if (!File.Exists(itemJson))
            {
                throw new FileNotFoundException(
                    $"{itemJson} not found — run the pipeline first: PackExtractor extract locale_tr + ProtoConverter item (docs/sprints/SPRINT_10-content-pipeline.md)");
            }

            if (!File.Exists(mobJson))
            {
                throw new FileNotFoundException($"{mobJson} not found — run ProtoConverter mob first");
            }

            EnsureFolder(GameDataFolder);
            EnsureFolder(IconDestRoot);

            // --- items ---------------------------------------------------------
            ItemDoc doc = JsonUtility.FromJson<ItemDoc>(File.ReadAllText(itemJson))
                ?? throw new InvalidDataException($"Cannot parse {itemJson}");

            var iconPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new ItemDef[doc.items.Length];
            for (int i = 0; i < doc.items.Length; i++)
            {
                ItemDto dto = doc.items[i];
                items[i] = new ItemDef
                {
                    Vnum = dto.vnum,
                    LocaleName = dto.localeName ?? "",
                    Type = dto.type,
                    SubType = dto.subType,
                    Weight = dto.weight,
                    Size = dto.size,
                    AntiFlags = dto.antiFlags,
                    Flags = dto.flags,
                    WearFlags = dto.wearFlags,
                    ImmuneFlag = dto.immuneFlag,
                    Gold = dto.gold,
                    ShopBuyPrice = dto.shopBuyPrice,
                    RefinedVnum = dto.refinedVnum,
                    RefineSet = dto.refineSet,
                    Values = dto.values ?? Array.Empty<long>(),
                    Sockets = dto.sockets ?? Array.Empty<long>(),
                    IconPath = dto.iconPath ?? "",
                };

                if (!string.IsNullOrEmpty(dto.iconPath))
                {
                    iconPaths.Add(dto.iconPath);
                }
            }

            // --- icons ---------------------------------------------------------
            // Two phases: copy everything first, refresh once so Unity sees
            // the new files, then apply import settings and load sprites
            // (LoadAssetAtPath returns null for un-imported files).
            int iconsImported = 0;
            int iconsMissing = 0;
            var copiedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string iconPath in iconPaths)
            {
                // iconPath is pack-virtual ("icon/item/00010.tga"); the
                // extraction rooted the pack at Extracted/icon/.
                string source = Path.Combine(iconRoot, iconPath);
                if (!File.Exists(source))
                {
                    iconsMissing++;
                    continue;
                }

                string relative = iconPath.StartsWith("icon/", StringComparison.OrdinalIgnoreCase)
                    ? iconPath[5..]
                    : iconPath;
                string dest = $"{IconDestRoot}/{relative}";
                EnsureFolder(Path.GetDirectoryName(dest)!.Replace('\\', '/'));

                if (!File.Exists(dest) || File.GetLastWriteTimeUtc(dest) < File.GetLastWriteTimeUtc(source))
                {
                    File.Copy(source, dest, overwrite: true);
                }

                copiedAssets.Add(dest);
            }

            if (copiedAssets.Count > 0)
            {
                AssetDatabase.Refresh();
            }

            var spriteByPath = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            foreach (string iconPath in iconPaths)
            {
                string relative = iconPath.StartsWith("icon/", StringComparison.OrdinalIgnoreCase)
                    ? iconPath[5..]
                    : iconPath;
                string dest = $"{IconDestRoot}/{relative}";
                if (!copiedAssets.Contains(dest))
                {
                    continue;
                }

                ImportSpriteSettings(dest);
                Sprite? sprite = AssetDatabase.LoadAssetAtPath<Sprite>(dest);
                if (sprite != null)
                {
                    spriteByPath[iconPath] = sprite;
                    iconsImported++;
                }
                else
                {
                    iconsMissing++;
                }
            }

            foreach (ItemDef item in items)
            {
                if (spriteByPath.TryGetValue(item.IconPath, out Sprite? sprite))
                {
                    item.Icon = sprite;
                }
            }

            // --- mobs ----------------------------------------------------------
            MobDoc mobDoc = JsonUtility.FromJson<MobDoc>(File.ReadAllText(mobJson))
                ?? throw new InvalidDataException($"Cannot parse {mobJson}");
            var mobs = new MobDef[mobDoc.mobs.Length];
            for (int i = 0; i < mobDoc.mobs.Length; i++)
            {
                MobDto dto = mobDoc.mobs[i];
                mobs[i] = new MobDef
                {
                    Vnum = dto.vnum,
                    LocaleName = dto.localeName ?? "",
                    Type = dto.type,
                    Rank = dto.rank,
                    BattleType = dto.battleType,
                    Level = dto.level,
                    Size = dto.size,
                    MaxHp = dto.maxHp,
                    Exp = dto.exp,
                    GoldMin = dto.goldMin,
                    GoldMax = dto.goldMax,
                    DamageMin = dto.damageMin,
                    DamageMax = dto.damageMax,
                    Def = dto.def,
                    AttackSpeed = dto.attackSpeed,
                    MovingSpeed = dto.movingSpeed,
                    Folder = dto.folder ?? "",
                };
            }

            // --- databases -----------------------------------------------------
            SaveDatabase<ItemDatabase, ItemDef>(GameDataFolder + "/ItemDatabase.asset", items);
            SaveDatabase<MobDatabase, MobDef>(GameDataFolder + "/MobDatabase.asset", mobs);

            string summary = $"[ProtoImporter] items={items.Length} ({iconsImported} icon sprite, {iconsMissing} missing), " +
                $"mobs={mobs.Length} -> {GameDataFolder}";
            Debug.Log(summary);
            Console.WriteLine(summary);
        }

        private static void ImportSpriteSettings(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            var ios = importer.GetPlatformTextureSettings("iPhone");
            ios.overridden = true;
            ios.format = TextureImporterFormat.ASTC_6x6;
            ios.maxTextureSize = 64;
            importer.SetPlatformTextureSettings(ios);

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.maxTextureSize = 64;
            importer.SetPlatformTextureSettings(android);

            importer.SaveAndReimport();
        }

        private static void SaveDatabase<TDatabase, TValue>(string assetPath, TValue[] values)
            where TDatabase : ScriptableObject
        {
            TDatabase? database = AssetDatabase.LoadAssetAtPath<TDatabase>(assetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<TDatabase>();
                AssetDatabase.CreateAsset(database, assetPath);
            }

            var field = typeof(TDatabase).GetField(
                typeof(TDatabase) == typeof(ItemDatabase) ? "Items" : "Mobs");
            field?.SetValue(database, values);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // --- JsonUtility DTOs (field names match the converter's camelCase) --

        [Serializable]
        private sealed class ItemDoc
        {
            public int count;
            public ItemDto[] items = Array.Empty<ItemDto>();
        }

        [Serializable]
        private sealed class ItemDto
        {
            public long vnum;
            public string localeName = "";
            public long type;
            public long subType;
            public long weight;
            public long size;
            public long antiFlags;
            public long flags;
            public long wearFlags;
            public long immuneFlag;
            public long gold;
            public long shopBuyPrice;
            public long[] values = Array.Empty<long>();
            public long[] sockets = Array.Empty<long>();
            public long refinedVnum;
            public long refineSet;
            public string iconPath = "";
        }

        [Serializable]
        private sealed class MobDoc
        {
            public int count;
            public MobDto[] mobs = Array.Empty<MobDto>();
        }

        [Serializable]
        private sealed class MobDto
        {
            public long vnum;
            public string localeName = "";
            public long type;
            public long rank;
            public long battleType;
            public long level;
            public long size;
            public long maxHp;
            public long exp;
            public long goldMin;
            public long goldMax;
            public long damageMin;
            public long damageMax;
            public long def;
            public long attackSpeed;
            public long movingSpeed;
            public string folder = "";
        }
    }
}
