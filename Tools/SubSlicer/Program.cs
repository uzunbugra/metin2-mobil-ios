using System.Text.Encodings.Web;
using System.Text.Json;
using Metin2.Tools.SubSlicer;

namespace Metin2.Tools.SubSlicer
{
    /// <summary>
    /// SP10-7 — .sub (subtexture) sprite slicer CLI. Parses Metin2 UI .sub
    /// files (text-format sprite rectangles over atlas DDS/TGA images) into
    /// JSON; the Unity side turns each record into a Sprite sliced from the
    /// atlas texture.
    ///
    /// Usage:
    ///   metin2-subslicer parse <inputDir> <out.json> [--recursive]
    ///
    ///   inputDir  directory containing .sub files (e.g. "Extracted/ETC/ymir work/ui")
    ///   out.json  output JSON path
    ///   --recursive  also scan subdirectories (off by default)
    ///
    /// Output schema:
    ///   {
    ///     "source": "<inputDir>",
    ///     "subCount": 784,
    ///     "skippedCount": 0,
    ///     "subs": [
    ///       {
    ///         "file": "<.sub path relative to inputDir>",
    ///         "image": "<atlas resource path, resolved like the client>",
    ///         "left": 0, "top": 0, "right": 512, "bottom": 68,
    ///         "width": 512, "height": 68
    ///       }, ...
    ///     ],
    ///     "skipped": [ { "file": "...", "reason": "..." } ]
    ///   }
    ///
    /// Error handling is fail-closed but batch-tolerant: malformed/deviant
    /// .sub files are skipped, counted and reported (in the JSON "skipped"
    /// list and on stderr); the run still succeeds if at least one file
    /// parses. Exit codes: 0 = ok, 1 = nothing parsed (or I/O error),
    /// 2 = usage error.
    ///
    /// The image path mirrors the client's resolution (EterLib/GrpSubImage.cpp:
    /// 102-123): "D:/Ymir Work/UI/" + lowercased image name for version 1.0
    /// (the client lowercases both tokens via stl_lowers, GrpSubImage.cpp:85-86;
    /// pack paths are lowercase on disk). Compare case-insensitively if in doubt.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 3 || !string.Equals(args[0], "parse", StringComparison.OrdinalIgnoreCase))
            {
                PrintUsage();
                return 2;
            }

            bool recursive = args.Any(a => string.Equals(a, "--recursive", StringComparison.OrdinalIgnoreCase));
            string inputDir = args[1];
            string outputPath = args[2];

            if (!Directory.Exists(inputDir))
            {
                Console.Error.WriteLine($"ERROR: input directory not found: {inputDir}");
                return 1;
            }

            try
            {
                return ParseAll(inputDir, outputPath, recursive);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
        }

        private static int ParseAll(string inputDir, string outputPath, bool recursive)
        {
            var searchOptions = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                MatchCasing = MatchCasing.CaseInsensitive,
            };

            string[] subFiles = Directory.GetFiles(inputDir, "*.sub", searchOptions);
            if (subFiles.Length == 0)
            {
                Console.Error.WriteLine($"ERROR: no .sub files under {inputDir}" + (recursive ? "" : " (try --recursive)"));
                return 1;
            }

            // Deterministic order regardless of filesystem enumeration order.
            Array.Sort(subFiles, StringComparer.Ordinal);

            string baseDir = Path.GetFullPath(inputDir);
            var subs = new List<object>(subFiles.Length);
            var skipped = new List<object>();
            var atlases = new HashSet<string>();

            foreach (string subFile in subFiles)
            {
                string relative = Path.GetRelativePath(baseDir, subFile).Replace('\\', '/');
                try
                {
                    SubImageRecord record = SubImage.Parse(subFile);
                    atlases.Add(record.Image);
                    subs.Add(new
                    {
                        file = relative,
                        image = record.Image,
                        left = record.Left,
                        top = record.Top,
                        right = record.Right,
                        bottom = record.Bottom,
                        width = record.Right - record.Left,
                        height = record.Bottom - record.Top,
                    });
                }
                catch (InvalidDataException ex)
                {
                    skipped.Add(new { file = relative, reason = ex.Message });
                }
            }

            var json = new
            {
                source = inputDir,
                subCount = subs.Count,
                skippedCount = skipped.Count,
                subs,
                skipped,
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // paths contain no HTML-sensitive chars
            };
            File.WriteAllText(outputPath, JsonSerializer.Serialize(json, options));

            foreach (var skip in skipped)
            {
                Console.Error.WriteLine($"SKIPPED: {skip}");
            }

            Console.WriteLine(
                $"parsed={subs.Count} skipped={skipped.Count} atlases={atlases.Count} -> {outputPath}");

            // Fail-closed: a run where nothing parsed is a failure even though
            // individual bad files were tolerated.
            return subs.Count > 0 ? 0 : 1;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine(
                "Usage:\n" +
                "  metin2-subslicer parse <inputDir> <out.json> [--recursive]");
        }
    }
}
