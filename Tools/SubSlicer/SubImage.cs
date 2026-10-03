using System.Text;

namespace Metin2.Tools.SubSlicer
{
    /// <summary>Parsed .sub record: one sprite rectangle over an atlas image.</summary>
    /// <param name="File">Path of the .sub file, relative to the input directory.</param>
    /// <param name="Image">Atlas resource path, resolved the way the client resolves it.</param>
    /// <param name="Left">Rectangle left edge in atlas pixels (GrpSubImage m_rect.left).</param>
    /// <param name="Top">Rectangle top edge in atlas pixels (GrpSubImage m_rect.top).</param>
    /// <param name="Right">Rectangle right edge in atlas pixels (GrpSubImage m_rect.right).</param>
    /// <param name="Bottom">Rectangle bottom edge in atlas pixels (GrpSubImage m_rect.bottom).</param>
    public sealed record SubImageRecord(
        string File,
        string Image,
        int Left,
        int Top,
        int Right,
        int Bottom);

    /// <summary>
    /// .sub (subtexture) parser — port of the client loader
    /// EterLib/GrpSubImage.cpp:65-133 (CGraphicSubImage::OnLoad) with its
    /// tokenizer EterBase/FileLoader.cpp:83-125 (CMemoryTextFileLoader::SplitLine)
    /// and line splitter EterBase/FileLoader.cpp:146-180 (Bind).
    ///
    /// Format — plain text, one "key value" pair per line:
    ///   title subImage
    ///   version 1.0
    ///   image "AtlasName.dds"
    ///   left 336
    ///   top 0
    ///   right 500
    ///   bottom 36
    ///
    /// Client parsing rules (kept for parity):
    ///   - Lines are split on \n, \r\n or lone \r (FileLoader.cpp:159-166);
    ///     a final unterminated line still counts as a line (FileLoader.cpp:179).
    ///   - Tokens are split on space/tab; a token may be wrapped in double
    ///     quotes, which allows spaces inside the value (FileLoader.cpp:101-115).
    ///   - A line whose tokens cannot be split (empty/whitespace-only, or an
    ///     unterminated quote) is skipped, NOT an error (GrpSubImage.cpp:79-80).
    ///   - A line that splits into a token count other than 2 rejects the
    ///     whole file (GrpSubImage.cpp:82-83).
    ///   - Both tokens are lowercased before the map lookup (GrpSubImage.cpp:85-88;
    ///     stl_lowers is ASCII tolower for this data — the files are pure ASCII).
    ///   - Unknown keys are stored but ignored (the map accepts any key;
    ///     only the 7 known keys are read, GrpSubImage.cpp:91-97).
    ///   - Coordinates go through atoi(): missing/garbage values become 0
    ///     (GrpSubImage.cpp:127-130) — emulated by <see cref="Atoi"/>.
    ///   - title must equal "subimage" (GrpSubImage.cpp:99-100).
    ///
    /// Atlas path resolution (GrpSubImage.cpp:102-123):
    ///   - version 2.0: image path is relative to the .sub file's directory.
    ///     The client only looks for a '\\' separator (find_last_of('\\'),
    ///     GrpSubImage.cpp:106); we normalize both separators.
    ///   - any other version (1.0 in practice): the default search path
    ///     "D:/Ymir Work/UI/" is prepended (GrpSubImage.cpp:7,122).
    /// </summary>
    public static class SubImage
    {
        /// <summary>Default atlas search path for non-2.0 files (GrpSubImage.cpp:7).</summary>
        public const string DefaultSearchPath = "D:/Ymir Work/UI/";

        /// <summary>Token delimiters (FileLoader.h:20, SplitLine default argument).</summary>
        private const string Delimiters = " \t";

        /// <summary>
        /// Parses one .sub file.
        /// </summary>
        /// <param name="subPath">Path of the .sub file (used for v2.0 relative resolution).</param>
        /// <returns>The parsed record.</returns>
        /// <exception cref="InvalidDataException">The file deviates from the format.</exception>
        public static SubImageRecord Parse(string subPath)
        {
            // Latin-1: lossless byte→char mapping, mirrors the client's byte-oriented
            // CMemoryTextFileLoader (which copies >= 0x80 bytes verbatim,
            // FileLoader.cpp:168-172). The observed .sub corpus is pure ASCII.
            string text = File.ReadAllText(subPath, Encoding.Latin1);

            var tokenMap = new Dictionary<string, string>();
            foreach (string line in SplitLines(text))
            {
                if (!SplitLine(line, out string[] tokens))
                {
                    continue; // unsplittable line — skipped, not an error (GrpSubImage.cpp:79-80)
                }

                if (tokens.Length != 2)
                {
                    throw new InvalidDataException(
                        $"line does not have exactly 2 tokens: \"{line}\" (GrpSubImage.cpp:82-83)");
                }

                // stl_lowers on both tokens (GrpSubImage.cpp:85-86).
                tokenMap[tokens[0].ToLowerInvariant()] = tokens[1].ToLowerInvariant();
            }

            string title = GetOrDefault(tokenMap, "title");
            if (title != "subimage")
            {
                throw new InvalidDataException(
                    $"title is \"{title}\", expected \"subimage\" (GrpSubImage.cpp:99-100)");
            }

            string version = GetOrDefault(tokenMap, "version");
            string image = GetOrDefault(tokenMap, "image");
            if (image.Length == 0)
            {
                // The client would load "D:/Ymir Work/UI/" itself as a resource and
                // fail later; reject here so the bad file is visible in the report.
                throw new InvalidDataException("image key missing or empty");
            }

            int left = Atoi(GetOrDefault(tokenMap, "left"));
            int top = Atoi(GetOrDefault(tokenMap, "top"));
            int right = Atoi(GetOrDefault(tokenMap, "right"));
            int bottom = Atoi(GetOrDefault(tokenMap, "bottom"));

            if (right < left || bottom < top)
            {
                throw new InvalidDataException(
                    $"degenerate rect ({left},{top})-({right},{bottom}) — width/height would be negative");
            }

            string atlas = version == "2.0"
                ? ResolveRelative(subPath, image)
                : DefaultSearchPath + image;

            return new SubImageRecord(subPath, atlas, left, top, right, bottom);
        }

        /// <summary>
        /// Line splitter — port of CMemoryTextFileLoader::Bind
        /// (FileLoader.cpp:146-180): splits on \n, \r\n or lone \r, keeps the
        /// final unterminated line.
        /// </summary>
        private static List<string> SplitLines(string text)
        {
            var lines = new List<string>();
            var current = new StringBuilder();

            int pos = 0;
            while (pos < text.Length)
            {
                char c = text[pos++];
                if (c == '\n' || c == '\r')
                {
                    if (pos < text.Length && (text[pos] == '\n' || text[pos] == '\r'))
                    {
                        pos++;
                    }

                    lines.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            lines.Add(current.ToString());
            return lines;
        }

        /// <summary>
        /// Tokenizer — port of CMemoryTextFileLoader::SplitLine
        /// (FileLoader.cpp:83-125): splits on space/tab, honours double-quoted
        /// tokens. Returns false when the line has no tokens or an unterminated
        /// quote (the caller then skips the line).
        /// </summary>
        private static bool SplitLine(string line, out string[] tokens)
        {
            var result = new List<string>();
            int basePos = 0;

            while (true)
            {
                int beginPos = IndexOfNotOf(line, basePos);
                if (beginPos < 0)
                {
                    // No non-delimiter content left (covers empty/whitespace-only lines).
                    if (result.Count == 0)
                    {
                        tokens = Array.Empty<string>();
                        return false;
                    }

                    break;
                }

                int endPos;
                if (line[beginPos] == '"')
                {
                    beginPos++;
                    endPos = line.IndexOf('"', beginPos);
                    if (endPos < 0)
                    {
                        tokens = Array.Empty<string>();
                        return false; // unterminated quote (FileLoader.cpp:106-107)
                    }

                    basePos = endPos + 1;
                }
                else
                {
                    endPos = IndexOfAnyOf(line, beginPos);
                    basePos = endPos; // -1 → rest of line is one token (npos semantics)
                }

                // substr semantics: endPos == -1 (npos) → token runs to end of line.
                int tokenEnd = endPos < 0 ? line.Length : endPos;
                result.Add(line[beginPos..tokenEnd]);

                // Trailing-delimiter check (FileLoader.cpp:119-121): stop when only
                // delimiters remain after the current token.
                if (IndexOfNotOf(line, Math.Min(basePos, line.Length)) < 0)
                {
                    break;
                }

                if (basePos < 0 || basePos >= line.Length)
                {
                    break;
                }
            }

            tokens = result.ToArray();
            return true;
        }

        /// <summary>find_first_not_of(Delimiters) — -1 when none (npos as int).</summary>
        private static int IndexOfNotOf(string line, int start)
        {
            for (int i = Math.Max(start, 0); i < line.Length; i++)
            {
                if (!Delimiters.Contains(line[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>find_first_of(Delimiters) — -1 when none (npos semantics).</summary>
        private static int IndexOfAnyOf(string line, int start)
        {
            for (int i = Math.Max(start, 0); i < line.Length; i++)
            {
                if (Delimiters.Contains(line[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// atoi() emulation for coordinate values (GrpSubImage.cpp:127-130):
        /// skips leading whitespace, takes an optional sign, consumes digits;
        /// anything else yields 0 (or the value parsed so far).
        /// </summary>
        private static int Atoi(string value)
        {
            int i = 0;
            while (i < value.Length && char.IsWhiteSpace(value[i]))
            {
                i++;
            }

            int sign = 1;
            if (i < value.Length && (value[i] == '+' || value[i] == '-'))
            {
                sign = value[i] == '-' ? -1 : 1;
                i++;
            }

            long result = 0;
            while (i < value.Length && value[i] >= '0' && value[i] <= '9')
            {
                result = result * 10 + (value[i] - '0');
                if (result > int.MaxValue)
                {
                    result = int.MaxValue; // saturate; C atoi overflow is UB in practice
                }

                i++;
            }

            return (int)(sign * result);
        }

        /// <summary>std::map operator[] semantics: missing key → empty string.</summary>
        private static string GetOrDefault(Dictionary<string, string> map, string key)
        {
            return map.TryGetValue(key, out string? value) ? value : string.Empty;
        }

        /// <summary>
        /// v2.0 image resolution (GrpSubImage.cpp:103-118): the image path is
        /// relative to the .sub file's directory. The client only honours a
        /// '\\' separator in the resource path; we normalise both separators
        /// so extracted trees with '/' paths resolve the same way.
        /// </summary>
        private static string ResolveRelative(string subPath, string image)
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(subPath));
            string normalized = image.Replace('\\', '/');
            return dir is null ? normalized : $"{dir.Replace('\\', '/')}/{normalized}";
        }
    }
}
