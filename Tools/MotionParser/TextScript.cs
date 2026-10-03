using System.Text;

namespace Metin2.Tools.MotionParser;

/// <summary>
/// Port of the client's CTextFileLoader script format
/// (fulldosya/source/Client Source/source/GameLib/TextFileLoader.cpp).
///
/// Format observed in .msa / .msm / motlist files:
///   - whitespace-separated `Key Value...` lines inside the current node;
///   - `Group &lt;Name&gt;` followed by a `{` line opens a child node,
///     a lone `}` closes it;
///   - `List &lt;Name&gt;` followed by `{` opens a raw token-row block
///     (e.g. `List HitPosition` in .msa attack data). The client flattens
///     such lists into a single token vector (GameType.cpp:57-75,
///     NRaceData::THitData::Load), so we register the concatenated tokens
///     as a multi-value token on the parent as well;
///   - double-quoted strings may contain spaces;
///   - keys are matched case-insensitively.
/// </summary>
public sealed class ScriptNode
{
    private readonly Dictionary<string, string[]> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; init; } = "";
    public bool IsList { get; init; }
    public List<string[]> Rows { get; } = new();
    public List<ScriptNode> Children { get; } = new();

    /// <summary>All token values registered for <paramref name="key"/> (null if absent).</summary>
    public string[]? GetValues(string key) => _tokens.TryGetValue(key, out var v) ? v : null;

    public string? GetString(string key)
    {
        var v = GetValues(key);
        return v is { Length: >= 1 } ? v[0] : null;
    }

    public float GetFloat(string key, float? fallback = null)
    {
        var s = GetString(key);
        if (s != null && float.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var f))
            return f;
        if (fallback.HasValue)
            return fallback.Value;
        throw MotionParseException.MissingToken(key, Name);
    }

    public int GetInt(string key, int? fallback = null)
    {
        var s = GetString(key);
        if (s != null && int.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var i))
            return i;
        if (fallback.HasValue)
            return fallback.Value;
        throw MotionParseException.MissingToken(key, Name);
    }

    /// <summary>GetTokenBoolean port: 0/1 integer tokens (non-zero = true).</summary>
    public bool GetBool(string key, bool? fallback = null)
    {
        var s = GetString(key);
        if (s != null && int.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var i))
            return i != 0;
        if (fallback.HasValue)
            return fallback.Value;
        throw MotionParseException.MissingToken(key, Name);
    }

    /// <summary>GetTokenVector/GetTokenPosition port: every value of the key parsed as float.</summary>
    public float[] GetFloatVector(string key)
    {
        var v = GetValues(key);
        if (v is null || v.Length == 0)
            throw MotionParseException.MissingToken(key, Name);
        var result = new float[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            if (!float.TryParse(v[i], System.Globalization.CultureInfo.InvariantCulture, out result[i]))
                throw new MotionParseException($"node '{Name}': token '{key}' value '{v[i]}' is not a number");
        }
        return result;
    }

    public ScriptNode? GetChild(string name) =>
        Children.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Children whose name starts with <paramref name="prefix"/> — mirrors
    /// CTextFileLoader::SetChildNode(name, index), which matches indexed group
    /// names like "Event00" / "HitData00" / "SphereData00".
    /// </summary>
    public IReadOnlyList<ScriptNode> GetChildren(string prefix) =>
        Children.Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

    internal void AddToken(string key, string[] values)
    {
        // CTextFileLoader keeps the first occurrence; later duplicates are ignored.
        if (!_tokens.ContainsKey(key))
            _tokens[key] = values;
    }
}

/// <summary>
/// Tokenizer/parser producing a <see cref="ScriptNode"/> tree from a
/// CTextFileLoader-style script file. Files are read as Latin-1 so CP1254
/// bytes survive verbatim (asset names are ASCII).
/// </summary>
public static class ScriptText
{
    public static ScriptNode ParseFile(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path, Encoding.Latin1);
        }
        catch (Exception ex)
        {
            throw new MotionParseException($"cannot read '{path}': {ex.Message}");
        }

        var root = Parse(text);
        FlattenLists(root);
        return root;
    }

    private static ScriptNode Parse(string text)
    {
        var root = new ScriptNode();
        var stack = new Stack<ScriptNode>();
        stack.Push(root);

        int lineNo = 0;
        foreach (var rawLine in text.Split('\n'))
        {
            lineNo++;
            var line = rawLine.TrimEnd('\r');
            var tokens = Tokenize(line, lineNo);

            if (tokens.Count == 0)
                continue;

            // Lone "{" opens the block announced by the previous Group/List line.
            if (tokens.Count == 1 && tokens[0] == "{")
                continue;

            // Closing brace(s) at line start end the current node.
            while (tokens.Count > 0 && tokens[0] == "}")
            {
                tokens.RemoveAt(0);
                if (stack.Count > 1)
                    stack.Pop();
            }

            if (tokens.Count == 0)
                continue;

            var current = stack.Peek();

            if (tokens[0].Equals("Group", StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Count is < 2 or > 3 || (tokens.Count == 3 && tokens[2] != "{"))
                    throw new MotionParseException($"line {lineNo}: malformed 'Group' header");
                var child = new ScriptNode { Name = tokens[1] };
                current.Children.Add(child);
                stack.Push(child);
                continue;
            }

            if (tokens[0].Equals("List", StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Count is < 2 or > 3 || (tokens.Count == 3 && tokens[2] != "{"))
                    throw new MotionParseException($"line {lineNo}: malformed 'List' header");
                var child = new ScriptNode { Name = tokens[1], IsList = true };
                current.Children.Add(child);
                stack.Push(child);
                continue;
            }

            if (current.IsList)
            {
                current.Rows.Add(tokens.ToArray());
                continue;
            }

            // Plain `Key Value...` line inside the current node.
            current.AddToken(tokens[0], tokens.Skip(1).ToArray());
        }

        if (stack.Count != 1)
            throw new MotionParseException("unbalanced braces: unexpected end of file inside a block");

        return root;
    }

    /// <summary>
    /// Register every `List &lt;Name&gt;` block as a flattened multi-value token
    /// on its parent node, mirroring GetTokenVector over list blocks
    /// (GameType.cpp:57-75).
    /// </summary>
    private static void FlattenLists(ScriptNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.IsList)
            {
                var flat = child.Rows.SelectMany(r => r).ToArray();
                if (flat.Length > 0)
                    node.AddToken(child.Name, flat);
            }
            else
            {
                FlattenLists(child);
            }
        }
    }

    /// <summary>Split a line into whitespace-separated tokens; double-quoted strings keep spaces.</summary>
    private static List<string> Tokenize(string line, int lineNo)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            while (i < line.Length && char.IsWhiteSpace(line[i]))
                i++;
            if (i >= line.Length)
                break;

            if (line[i] == '"')
            {
                int end = line.IndexOf('"', i + 1);
                if (end < 0)
                    throw new MotionParseException($"line {lineNo}: unterminated string literal");
                tokens.Add(line[(i + 1)..end]);
                i = end + 1;
            }
            else
            {
                int start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]))
                    i++;
                tokens.Add(line[start..i]);
            }
        }

        return tokens;
    }
}

/// <summary>Fail-closed parse error with source context.</summary>
public sealed class MotionParseException : Exception
{
    public MotionParseException(string message) : base(message) { }

    public static MotionParseException MissingToken(string key, string nodeName) =>
        new($"node '{nodeName}': required token '{key}' is missing or malformed");
}
