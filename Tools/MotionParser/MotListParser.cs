using System.Text;

namespace Metin2.Tools.MotionParser;

/// <summary>
/// One motlist.txt entry: `MODE TYPE FILE [PERCENT]`.
/// </summary>
/// <param name="Mode">Mode token (e.g. "GENERAL"). Parsed by the client via
/// sscanf (RaceManager.cpp:270) but always registered under
/// CRaceMotionData::MODE_GENERAL (RaceManager.cpp:249,305) — kept for reference.</param>
/// <param name="Type">Raw motion type token, used as the JSON key (e.g. "WAIT1").</param>
/// <param name="Canonical">Resolved CRaceMotionData motion name (e.g. "NAME_WAIT");
/// null when the type is unknown even after suffix truncation (the client
/// silently skips such lines, RaceManager.cpp:296-297).</param>
/// <param name="MsaFile">Motion script file, relative to the race root.</param>
/// <param name="Percent">Selection weight (client: RegisterMotionData, RaceManager.cpp:305).</param>
/// <param name="ResolvedByTruncation">True when the type only matched after
/// dropping up to 2 trailing characters (RaceManager.cpp:278-294).</param>
public sealed record MotListEntry(
    string Mode,
    string Type,
    string? Canonical,
    string MsaFile,
    int Percent,
    bool ResolvedByTruncation);

/// <summary>
/// motlist.txt parser — mirrors CRaceManager::__LoadRaceMotionList
/// (fulldosya/source/Client Source/source/GameLib/RaceManager.cpp:180-326).
///
/// Line format (RaceManager.cpp:270, sscanf "%s %s %s %d"):
///   MODE TYPE FILE [PERCENT]   e.g.  "GENERAL WAIT 00.msa 65"
///
/// The type token maps to a canonical motion name via the exact table at
/// RaceManager.cpp:189-237; unknown tokens are retried with up to 2 trailing
/// characters removed so "WAIT4" resolves to "WAIT" (RaceManager.cpp:276-298).
/// </summary>
public static class MotListParser
{
    /// <summary>Exact type → canonical name table (RaceManager.cpp:189-237).</summary>
    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SPAWN"] = "NAME_SPAWN",
        ["WAIT"] = "NAME_WAIT",
        ["WAIT1"] = "NAME_WAIT",
        ["WAIT2"] = "NAME_WAIT",
        ["WALK"] = "NAME_WALK",
        ["WALK1"] = "NAME_WALK",
        ["WALK2"] = "NAME_WALK",
        ["RUN"] = "NAME_RUN",
        ["RUN1"] = "NAME_RUN",
        ["RUN2"] = "NAME_RUN",
        ["STOP"] = "NAME_STOP",
        ["DEAD"] = "NAME_DEAD",
        ["COMBO_ATTACK"] = "NAME_COMBO_ATTACK_1",
        ["COMBO_ATTACK1"] = "NAME_COMBO_ATTACK_2",
        ["COMBO_ATTACK2"] = "NAME_COMBO_ATTACK_3",
        ["NORMAL_ATTACK"] = "NAME_NORMAL_ATTACK",
        ["NORMAL_ATTACK1"] = "NAME_NORMAL_ATTACK",
        ["NORMAL_ATTACK2"] = "NAME_NORMAL_ATTACK",
        ["FRONT_DAMAGE"] = "NAME_DAMAGE",
        ["FRONT_DAMAGE1"] = "NAME_DAMAGE",
        ["FRONT_DAMAGE2"] = "NAME_DAMAGE",
        ["FRONT_DAMAGE3"] = "NAME_DAMAGE",
        ["FRONT_DEAD"] = "NAME_DEAD",
        ["FRONT_DEAD1"] = "NAME_DEAD",
        ["FRONT_DEAD2"] = "NAME_DEAD",
        ["FRONT_KNOCKDOWN"] = "NAME_DAMAGE_FLYING",
        ["FRONT_KNOCKDOWN1"] = "NAME_DAMAGE_FLYING",
        ["FRONT_STANDUP"] = "NAME_STAND_UP",
        ["FRONT_STANDUP1"] = "NAME_STAND_UP",
        ["BACK_DAMAGE"] = "NAME_DAMAGE_BACK",
        ["BACK_DAMAGE1"] = "NAME_DAMAGE_BACK",
        ["BACK_DEAD"] = "NAME_DEAD_BACK",
        ["BACK_DEAD1"] = "NAME_DEAD_BACK",
        ["BACK_DEAD2"] = "NAME_DEAD_BACK",
        ["BACK_KNOCKDOWN"] = "NAME_DAMAGE_FLYING_BACK",
        ["BACK_KNOCKDOWN1"] = "NAME_DAMAGE_FLYING_BACK",
        ["BACK_STANDUP"] = "NAME_STAND_UP_BACK",
        ["BACK_STANDUP1"] = "NAME_STAND_UP_BACK",
        ["SPECIAL"] = "NAME_SPECIAL_1",
        ["SPECIAL1"] = "NAME_SPECIAL_2",
        ["SPECIAL2"] = "NAME_SPECIAL_3",
        ["SPECIAL3"] = "NAME_SPECIAL_4",
        ["SPECIAL4"] = "NAME_SPECIAL_5",
        ["SPECIAL5"] = "NAME_SPECIAL_6",
        // SKILL1..5 map to NAME_SKILL+121..125 (RaceManager.cpp:233-237).
        ["SKILL1"] = "NAME_SKILL+121",
        ["SKILL2"] = "NAME_SKILL+122",
        ["SKILL3"] = "NAME_SKILL+123",
        ["SKILL4"] = "NAME_SKILL+124",
        ["SKILL5"] = "NAME_SKILL+125",
    };

    /// <summary>Maximum trailing characters stripped when resolving an unknown type (RaceManager.cpp:278).</summary>
    private const int CutLengthLimit = 2;

    public static List<MotListEntry> Parse(string motListPath)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(motListPath, Encoding.Latin1);
        }
        catch (Exception ex)
        {
            throw new MotionParseException($"cannot read '{motListPath}': {ex.Message}");
        }

        var entries = new List<MotListEntry>();

        int lineNo = 0;
        foreach (var rawLine in lines)
        {
            lineNo++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length is < 3 or > 4)
                throw new MotionParseException($"{motListPath}:{lineNo}: expected 'MODE TYPE FILE [PERCENT]', got {tokens.Length} token(s)");

            var mode = tokens[0];
            var type = tokens[1];
            var file = tokens[2];
            int percent = 0;
            if (tokens.Length == 4 &&
                !int.TryParse(tokens[3], System.Globalization.CultureInfo.InvariantCulture, out percent))
            {
                throw new MotionParseException($"{motListPath}:{lineNo}: percent '{tokens[3]}' is not an integer");
            }

            var (canonical, truncated) = ResolveType(type);
            if (canonical is null)
            {
                // The client skips unknown types (RaceManager.cpp:296-297). We keep
                // them (marked unrecognized) instead of silently dropping data.
                Console.Error.WriteLine(
                    $"WARNING: {motListPath}:{lineNo}: unknown motion type '{type}' (kept with canonical=null)");
            }

            entries.Add(new MotListEntry(mode, type, canonical, file, percent, truncated));
        }

        return entries;
    }

    private static (string? Canonical, bool Truncated) ResolveType(string type)
    {
        if (TypeMap.TryGetValue(type, out var canonical))
            return (canonical, false);

        // Suffix truncation fallback (RaceManager.cpp:278-294): WAIT4 -> WAIT.
        if (CutLengthLimit < type.Length + 1)
        {
            for (int i = 1; i <= CutLengthLimit; i++)
            {
                var truncated = type[..^i];
                if (TypeMap.TryGetValue(truncated, out canonical))
                    return (canonical, true);
            }
        }

        return (null, false);
    }
}
