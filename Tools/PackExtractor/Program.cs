using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Metin2.Tools.PackExtractor
{
    /// <summary>
    /// SP10-1 — EIX/EPK pack extractor CLI.
    ///
    /// Usage:
    ///   metin2-packextractor list &lt;pack.eix&gt; [--filter substr]
    ///   metin2-packextractor get &lt;pack.eix&gt; &lt;filename&gt; &lt;outfile&gt;
    ///   metin2-packextractor extract &lt;pack.eix&gt; &lt;outdir&gt; [--filter substr] [--types 0,1,2]
    ///   metin2-packextractor report &lt;pack.eix&gt; [&lt;report.json&gt;]
    ///
    /// The .eix path alone identifies the pack; the .epk is resolved from it.
    /// Output paths are sanitized against traversal (guide §7.1); original
    /// pack files are only ever read. Extracted data is Ymir/Webzen
    /// copyrighted — keep it local (ADR-0003).
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return 2;
            }

            try
            {
                string command = args[0].ToLowerInvariant();
                string eixPath = args[1];
                var options = ParseOptions(args.Skip(2).ToArray());

                string epkPath = Path.ChangeExtension(eixPath, ".epk");
                if (!File.Exists(eixPath) || !File.Exists(epkPath))
                {
                    Console.Error.WriteLine($"Pack not found: {eixPath} (+ .epk)");
                    return 2;
                }

                var reader = new EterPackReader(eixPath, epkPath);

                switch (command)
                {
                    case "list":
                        return List(reader, options);
                    case "get":
                    {
                        if (args.Length < 4)
                        {
                            PrintUsage();
                            return 2;
                        }

                        return Get(reader, args[2], args[3]);
                    }

                    case "extract":
                    {
                        if (args.Length < 3)
                        {
                            PrintUsage();
                            return 2;
                        }

                        return Extract(reader, args[2], options);
                    }

                    case "dumpindex":
                    {
                        if (args.Length < 3)
                        {
                            PrintUsage();
                            return 2;
                        }

                        return DumpIndex(reader, args[2]);
                    }
                    case "report":
                        return Report(reader, args.Length >= 3 ? args[2] : null);
                    default:
                        PrintUsage();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
        }

        private static int List(EterPackReader reader, Dictionary<string, string> options)
        {
            string? filter = options.TryGetValue("filter", out string? f) ? f : null;
            int shown = 0;
            foreach (EterPackReader.PackEntry entry in reader.Entries)
            {
                if (filter != null && !entry.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Console.WriteLine(
                    $"{entry.FileName}\ttype={entry.CompressedType}\treal={entry.RealDataSize}\tstored={entry.DataSize}");
                shown++;
            }

            Console.Error.WriteLine(
                $"[index: {(reader.IndexEncrypted ? "MCOZ/encrypted" : "plain")}, " +
                $"{reader.IndexCount} slots, {reader.Entries.Count} files; shown {shown}]");
            return 0;
        }

        private static int Get(EterPackReader reader, string fileName, string outFile)
        {
            EterPackReader.PackEntry? entry = reader.Entries.FirstOrDefault(
                e => string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                Console.Error.WriteLine($"File not in pack: {fileName}");
                return 1;
            }

            byte[] data = reader.ReadEntry(entry);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
            File.WriteAllBytes(outFile, data);
            Console.WriteLine($"{entry.FileName} -> {outFile} ({data.Length} B, type {entry.CompressedType})");
            return 0;
        }

        private static int Extract(EterPackReader reader, string outDir, Dictionary<string, string> options)
        {
            string? filter = options.TryGetValue("filter", out string? f) ? f : null;
            var allowedTypes = new HashSet<byte>(new[] { (byte)0, (byte)1, (byte)2 });
            if (options.TryGetValue("types", out string? types))
            {
                allowedTypes = new HashSet<byte>(
                    types.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => byte.Parse(t.Trim())));
            }

            int extracted = 0, skipped = 0, failed = 0;
            string outRoot = Path.GetFullPath(outDir);

            foreach (EterPackReader.PackEntry entry in reader.Entries)
            {
                if (filter != null && !entry.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!allowedTypes.Contains(entry.CompressedType))
                {
                    skipped++;
                    Console.Error.WriteLine($"SKIP (type {entry.CompressedType}): {entry.FileName}");
                    continue;
                }

                try
                {
                    byte[] data = reader.ReadEntry(entry);
                    string relative = SanitizeVirtualPath(entry.FileName);
                    string target = Path.Combine(outRoot, relative);
                    string? directory = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllBytes(target, data);
                    extracted++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.Error.WriteLine($"FAIL: {entry.FileName}: {ex.Message}");
                }
            }

            Console.WriteLine($"extracted={extracted} skipped={skipped} failed={failed} -> {outRoot}");
            return failed == 0 ? 0 : 1;
        }

        private static int DumpIndex(EterPackReader reader, string outFile)
        {
            // Debug aid: writes the raw (decrypted) index content so the
            // entry layout can be inspected with a hex editor.
            byte[] eix = File.ReadAllBytes(reader.IndexPath);
            byte[] index = reader.IndexEncrypted
                ? Mcoz.Decompress(eix, EterPackReader.IndexKey)
                : eix;
            File.WriteAllBytes(outFile, index);
            Console.WriteLine($"index ({index.Length} B) -> {outFile}");
            return 0;
        }

        private static int Report(EterPackReader reader, string? reportPath)
        {
            var byType = reader.Entries
                .GroupBy(e => e.CompressedType)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    compressedType = g.Key,
                    count = g.Count(),
                    totalRealBytes = (long)g.Sum(e => (double)e.RealDataSize),
                })
                .ToArray();

            var byExtension = reader.Entries
                .GroupBy(e => Path.GetExtension(e.FileName).ToLowerInvariant())
                .OrderByDescending(g => g.Count())
                .Select(g => new { extension = string.IsNullOrEmpty(g.Key) ? "(none)" : g.Key, count = g.Count() })
                .ToArray();

            var report = new
            {
                indexPath = reader.IndexPath,
                indexEncrypted = reader.IndexEncrypted,
                indexSlots = reader.IndexCount,
                files = reader.Entries.Count,
                totalRealBytes = (long)reader.Entries.Sum(e => (double)e.RealDataSize),
                byCompressedType = byType,
                byExtension,
            };

            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
            });

            if (reportPath == null)
            {
                Console.WriteLine(json);
            }
            else
            {
                File.WriteAllText(reportPath, json);
                Console.WriteLine($"report -> {reportPath}");
            }

            return 0;
        }

        /// <summary>
        /// Maps a pack's virtual path (e.g. "d:/ymir work/pc/warrior/warrior.msm",
        /// "icon/item/00000.tga") to a safe relative filesystem path. Rejects
        /// traversal and absolute components (guide §7.1).
        /// </summary>
        private static string SanitizeVirtualPath(string virtualPath)
        {
            string path = virtualPath.Replace('\\', '/');

            // Strip a Windows drive prefix ("d:").
            if (path.Length >= 2 && path[1] == ':')
            {
                path = path[2..];
            }

            path = path.TrimStart('/');

            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Any(p => p == ".." || p == "."))
            {
                throw new InvalidDataException($"Unsafe virtual path rejected: {virtualPath}");
            }

            return string.Join(Path.DirectorySeparatorChar, parts);
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                if (args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    options[args[i][2..]] = args[i + 1];
                }
            }

            return options;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine(
                "Usage:\n" +
                "  metin2-packextractor list <pack.eix> [--filter substr]\n" +
                "  metin2-packextractor get <pack.eix> <filename> <outfile>\n" +
                "  metin2-packextractor extract <pack.eix> <outdir> [--filter substr] [--types 0,1,2]\n" +
                "  metin2-packextractor report <pack.eix> [<report.json>]");
        }
    }
}
