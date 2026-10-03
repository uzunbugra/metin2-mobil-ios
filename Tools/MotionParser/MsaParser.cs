namespace Metin2.Tools.MotionParser;

/// <summary>
/// .msa (MotionData script) parser + race-level catalog builder.
///
/// Mirrors CRaceMotionData::LoadMotionData
/// (fulldosya/source/Client Source/source/GameLib/RaceMotionData.cpp:309-472):
///   ScriptType            MotionData
///   MotionFileName        "d:/ymir work/.../xxx.gr2"
///   MotionDuration        2.666667
///   Accumulation          0.00  0.00  0.00            (optional, 3 floats)
///   Group ComboInputData  { PreInputTime DirectInputTime InputLimitTime }
///   Group AttackingData   { attack data + HitDataNN groups (GameType.cpp:80-120) }
///   Group LoopData        { MotionLoopCount LoopCancelEnable LoopStartTime LoopEndTime }
///   Group MotionEventData { MotionEventDataCount + Group EventNN { ... } }
///
/// Event blocks carry a type id (RaceMotionData.h:159-175 EMotionEventType) and
/// type-specific fields (GameLib/RaceMotionDataEvent.h). Event frame numbers use
/// the client's game FPS of 60 (GameType.cpp:5, RaceMotionData.cpp:311,450).
/// </summary>
public static class MsaParser
{
    /// <summary>g_fGameFPS (GameType.cpp:5); events store dwFrame = startingTime / (1/FPS).</summary>
    private const float GameFps = 60.0f;

    /// <summary>EMotionEventType (RaceMotionData.h:159-175).</summary>
    private static readonly Dictionary<int, string> EventTypeNames = new()
    {
        [0] = "NONE",
        [1] = "EFFECT",
        [2] = "SCREEN_WAVING",
        [3] = "SCREEN_FLASHING",
        [4] = "SPECIAL_ATTACKING",
        [5] = "SOUND",
        [6] = "FLY",
        [7] = "CHARACTER_SHOW",
        [8] = "CHARACTER_HIDE",
        [9] = "WARP",
        [10] = "EFFECT_TO_TARGET",
    };

    /// <summary>
    /// Parse a whole race root: motlist.txt + every referenced .msa file.
    /// Returns the JSON-ready catalog:
    /// { race, source, stats, motlist: { TYPE: { mode, canonical, percent, msa,
    ///   duration, accumulation, isLoop, loop, isCombo, combo, isAttacking,
    ///   attacking, events } } }.
    /// Fail-closed: any missing file or malformed token throws (no partial output).
    /// </summary>
    public static Dictionary<string, object?> ParseRace(string raceRootDir)
    {
        var raceRoot = Path.GetFullPath(raceRootDir);
        if (!Directory.Exists(raceRoot))
            throw new MotionParseException($"race root not found: '{raceRoot}'");

        var motListPath = Path.Combine(raceRoot, "motlist.txt");
        if (!File.Exists(motListPath))
            throw new MotionParseException(
                $"motlist.txt not found in '{raceRoot}' (the client loads it as " +
                $"<raceRoot>/motlist.txt, RaceManager.cpp:116,168-171)");

        var entries = MotListParser.Parse(motListPath);
        var motlist = new Dictionary<string, object?>(StringComparer.Ordinal);
        int eventTotal = 0;

        foreach (var entry in entries)
        {
            var msaPath = Path.Combine(raceRoot, entry.MsaFile);
            if (!File.Exists(msaPath))
                throw new MotionParseException(
                    $"motion '{entry.Type}' references '{entry.MsaFile}' which does not exist under '{raceRoot}'");

            var msa = ParseMsa(msaPath);
            if (msa.Events is { Count: > 0 })
                eventTotal += msa.Events.Count;

            // Same raw type twice: last one wins, mirroring RegisterMotionData
            // overwriting the previous entry (RaceManager.cpp:305).
            motlist[entry.Type] = new Dictionary<string, object?>
            {
                ["mode"] = entry.Mode,
                ["canonical"] = entry.Canonical,
                ["resolvedByTruncation"] = entry.ResolvedByTruncation,
                ["percent"] = entry.Percent,
                ["msa"] = entry.MsaFile,
                ["duration"] = msa.Duration,
                ["accumulation"] = msa.Accumulation,
                ["isLoop"] = msa.Loop is not null,
                ["loop"] = msa.Loop,
                ["isCombo"] = msa.Combo is not null,
                ["combo"] = msa.Combo,
                ["isAttacking"] = msa.Attacking is not null,
                ["attacking"] = msa.Attacking,
                ["events"] = msa.Events ?? new List<object?>(),
            };
        }

        var raceName = new DirectoryInfo(raceRoot).Name;
        return new Dictionary<string, object?>
        {
            ["race"] = raceName,
            ["source"] = raceRoot,
            ["stats"] = new Dictionary<string, object?>
            {
                ["motionCount"] = motlist.Count,
                ["msaParsed"] = entries.Count,
                ["eventCount"] = eventTotal,
            },
            ["motlist"] = motlist,
        };
    }

    /// <summary>Parsed content of a single .msa file.</summary>
    private sealed record MsaData(
        float Duration,
        Dictionary<string, float>? Accumulation,
        Dictionary<string, object?>? Combo,
        Dictionary<string, object?>? Attacking,
        Dictionary<string, object?>? Loop,
        List<object?>? Events);

    private static MsaData ParseMsa(string path)
    {
        var root = ScriptText.ParseFile(path);

        var scriptType = root.GetString("scripttype");
        if (!string.Equals(scriptType, "MotionData", StringComparison.OrdinalIgnoreCase))
            throw new MotionParseException($"'{path}': ScriptType is '{scriptType}', expected 'MotionData'");

        // MotionFileName is validated but not exported (Unity gets the clip from
        // the .gr2 importer); duration/accumulation drive clip setup.
        if (root.GetString("motionfilename") is null)
            throw new MotionParseException($"'{path}': required token 'MotionFileName' is missing (RaceMotionData.cpp:324)");

        if (root.GetValues("motionduration") is not { Length: 1 })
            throw new MotionParseException($"'{path}': required token 'MotionDuration' is missing (RaceMotionData.cpp:327)");
        var duration = root.GetFloat("motionduration");

        // Accumulation: optional, must be exactly 3 floats (RaceMotionData.cpp:332-345).
        Dictionary<string, float>? accumulation = null;
        var accValues = root.GetValues("accumulation");
        if (accValues is not null)
        {
            if (accValues.Length != 3)
                throw new MotionParseException(
                    $"'{path}': 'Accumulation' must have 3 components, got {accValues.Length} (RaceMotionData.cpp:334)");
            accumulation = ToVec3(root.GetFloatVector("accumulation"));
        }

        Dictionary<string, object?>? combo = null;
        Dictionary<string, object?>? attacking = null;
        Dictionary<string, object?>? loop = null;
        List<object?>? events = null;

        foreach (var child in root.Children)
        {
            if (child.Name.Equals("comboinputdata", StringComparison.OrdinalIgnoreCase))
            {
                combo = new Dictionary<string, object?>
                {
                    ["preInputTime"] = child.GetFloat("preinputtime"),
                    ["directInputTime"] = child.GetFloat("directinputtime"),
                    ["inputLimitTime"] = child.GetFloat("inputlimittime"),
                };
            }
            else if (child.Name.Equals("attackingdata", StringComparison.OrdinalIgnoreCase))
            {
                attacking = ParseAttackingData(child, path);
            }
            else if (child.Name.Equals("loopdata", StringComparison.OrdinalIgnoreCase))
            {
                loop = new Dictionary<string, object?>
                {
                    ["loopCount"] = child.GetInt("motionloopcount", -1),
                    ["cancelEnableSkill"] = child.GetBool("loopcancelenable", false),
                    ["startTime"] = child.GetFloat("loopstarttime"),
                    ["endTime"] = child.GetFloat("loopendtime"),
                };
            }
            else if (child.Name.Equals("motioneventdata", StringComparison.OrdinalIgnoreCase))
            {
                events = ParseMotionEvents(child, path);
            }
            else
            {
                throw new MotionParseException(
                    $"'{path}': unknown child node '{child.Name}' in MotionData (not in RaceMotionData.cpp:348-455)");
            }
        }

        return new MsaData(duration, accumulation, combo, attacking, loop, events);
    }

    /// <summary>
    /// NRaceData::LoadAttackData port (GameType.cpp:20-40). AttackType defaults
    /// to ATTACK_TYPE_SPLASH (=0, GameType.h:41); HitLimitCount defaults to 0.
    /// </summary>
    private static Dictionary<string, object?> ParseAttackScalars(ScriptNode node)
    {
        return new Dictionary<string, object?>
        {
            ["attackType"] = node.GetInt("attacktype", 0),
            ["hittingType"] = node.GetInt("hittingtype"),
            ["stiffenTime"] = node.GetFloat("stiffentime"),
            ["invisibleTime"] = node.GetFloat("invisibletime"),
            ["externalForce"] = node.GetFloat("externalforce"),
            ["hitLimitCount"] = node.GetInt("hitlimitcount", 0),
        };
    }

    /// <summary>
    /// NRaceData::LoadMotionAttackData port (GameType.cpp:80-120): attack
    /// scalars + MotionType + HitData groups. Used by the top-level
    /// AttackingData block (RaceMotionData.cpp:365-371).
    /// </summary>
    private static Dictionary<string, object?> ParseAttackingData(ScriptNode node, string path)
    {
        var hitData = new List<object?>();

        var hitGroups = node.GetChildren("hitdata");
        var hitCount = node.GetInt("hitdatacount", -1);
        if (hitCount < 0)
        {
            // No HitDataCount: a single inline HitData (GameType.cpp:96-102).
            hitData.Add(ParseHitData(node, path));
        }
        else
        {
            if (hitGroups.Count != hitCount)
                throw new MotionParseException(
                    $"'{path}': HitDataCount is {hitCount} but {hitGroups.Count} HitData group(s) found");
            foreach (var g in hitGroups)
                hitData.Add(ParseHitData(g, path));
        }

        var result = ParseAttackScalars(node);

        // MotionType, falling back to AttackingType (GameType.cpp:87-91).
        result["motionType"] = node.GetString("motiontype") is not null
            ? node.GetInt("motiontype")
            : node.GetInt("attackingtype");
        result["hitData"] = hitData;
        return result;
    }

    /// <summary>NRaceData::THitData::Load port (GameType.cpp:42-78).</summary>
    private static Dictionary<string, object?> ParseHitData(ScriptNode node, string path)
    {
        var hitData = new Dictionary<string, object?>
        {
            ["attackingStartTime"] = node.GetFloat("attackingstarttime"),
            ["attackingEndTime"] = node.GetFloat("attackingendtime"),
            ["attackingBone"] = node.GetString("attackingbone") ?? "",
            ["weaponLength"] = node.GetFloat("weaponlength", 0.0f),
        };

        // List HitPosition rows: 7 floats each — time, lastPos.xyz, pos.xyz
        // (GameType.cpp:57-75). Flattened into a token vector by ScriptText.
        var positions = new List<object?>();
        var vec = node.GetValues("hitposition");
        if (vec is { Length: > 0 })
        {
            if (vec.Length % 7 != 0)
                throw new MotionParseException(
                    $"'{path}': HitPosition must be rows of 7 floats (t last.xyz pos.xyz), got {vec.Length} value(s)");
            var floats = node.GetFloatVector("hitposition");
            for (int i = 0; i < floats.Length; i += 7)
            {
                positions.Add(new Dictionary<string, object?>
                {
                    ["time"] = floats[i],
                    ["lastPosition"] = ToVec3(floats, i + 1),
                    ["position"] = ToVec3(floats, i + 4),
                });
            }
        }

        hitData["hitPositions"] = positions;
        return hitData;
    }

    /// <summary>MotionEventData block: count + EventNN groups (RaceMotionData.cpp:388-454).</summary>
    private static List<object?> ParseMotionEvents(ScriptNode node, string path)
    {
        var eventCount = node.GetInt("motioneventdatacount");
        var eventGroups = node.GetChildren("event");
        if (eventGroups.Count != eventCount)
            throw new MotionParseException(
                $"'{path}': MotionEventDataCount is {eventCount} but {eventGroups.Count} Event group(s) found");

        var events = new List<object?>();
        for (int i = 0; i < eventGroups.Count; i++)
            events.Add(ParseEvent(eventGroups[i], i, path));

        return events;
    }

    private static Dictionary<string, object?> ParseEvent(ScriptNode node, int index, string path)
    {
        var type = node.GetInt("motioneventtype");
        var startingTime = node.GetFloat("startingtime");
        var duringTime = node.GetFloat("duringtime", 0.0f);

        var result = new Dictionary<string, object?>
        {
            ["index"] = index,
            ["type"] = type,
            ["typeName"] = EventTypeNames.TryGetValue(type, out var name)
                ? name
                : throw new MotionParseException(
                    $"'{path}': event {index} has unknown MotionEventType {type} (RaceMotionData.cpp:439-441)"),
            // dwFrame = startingTime / (1/60) (RaceMotionData.cpp:311,450).
            ["frame"] = (int)(startingTime / (1.0f / GameFps)),
            ["startingTime"] = startingTime,
            ["duringTime"] = duringTime,
        };

        switch (type)
        {
            case 1: // EFFECT (RaceMotionDataEvent.h:67-112)
                result["independent"] = node.GetBool("independentflag", false);
                result["attaching"] = node.GetBool("attachingenable");
                result["attachingBone"] = node.GetString("attachingbonename");
                result["following"] = node.GetBool("followingenable", false);
                result["effectFile"] = node.GetString("effectfilename");
                result["effectPosition"] = ToVec3(node.GetFloatVector("effectposition"), enforceLength: 3);
                break;

            case 2: // SCREEN_WAVING (RaceMotionDataEvent.h:23-49)
                result["power"] = node.GetInt("power");
                result["affectingRange"] = node.GetInt("affectingrange", 0);
                break;

            case 3: // SCREEN_FLASHING (RaceMotionDataEvent.h:52-64) — no fields
                break;

            case 4: // SPECIAL_ATTACKING (RaceMotionDataEvent.h:201-237):
                    // event variant uses attack scalars + collision only
                    // (LoadAttackData, GameType.cpp:20-40 — no MotionType/HitData)
                result["enableHitProcess"] = node.GetBool("enablehitprocess", true);
                result["attack"] = ParseAttackScalars(node);
                result["collision"] = ParseCollisionData(node, path);
                break;

            case 5: // SOUND (RaceMotionDataEvent.h:240-259)
                result["soundFile"] = node.GetString("soundfilename");
                break;

            case 6: // FLY (RaceMotionDataEvent.h:159-198)
                result["attaching"] = node.GetBool("attachingenable");
                result["attachingBone"] = node.GetString("attachingbonename");
                result["flyFile"] = node.GetString("flyfilename");
                result["flyPosition"] = ToVec3(node.GetFloatVector("flyposition"), enforceLength: 3);
                break;

            case 7: // CHARACTER_SHOW (RaceMotionDataEvent.h:262-269) — no fields
            case 8: // CHARACTER_HIDE (RaceMotionDataEvent.h:272-279) — no fields
            case 9: // WARP (RaceMotionDataEvent.h:282-289) — no fields
                break;

            case 10: // EFFECT_TO_TARGET (RaceMotionDataEvent.h:115-156)
                result["effectFile"] = node.GetString("effectfilename");
                result["effectPosition"] = ToVec3(node.GetFloatVector("effectposition"), enforceLength: 3);
                result["following"] = node.GetBool("followingenable", false);
                result["fishingEffect"] = node.GetBool("fishingeffectflag", false);
                break;

            default:
                throw new MotionParseException(
                    $"'{path}': event {index} has unsupported MotionEventType {type} (RaceMotionData.cpp:439-441)");
        }

        return result;
    }

    /// <summary>NRaceData::LoadCollisionData port (GameType.cpp:122-149).</summary>
    private static Dictionary<string, object?> ParseCollisionData(ScriptNode node, string path)
    {
        var sphereGroups = node.GetChildren("spheredata");
        var sphereCount = node.GetInt("spheredatacount");
        if (sphereGroups.Count != sphereCount)
            throw new MotionParseException(
                $"'{path}': SphereDataCount is {sphereCount} but {sphereGroups.Count} SphereData group(s) found");

        var spheres = new List<object?>();
        foreach (var g in sphereGroups)
        {
            spheres.Add(new Dictionary<string, object?>
            {
                ["radius"] = g.GetFloat("radius"),
                ["position"] = ToVec3(g.GetFloatVector("position"), enforceLength: 3),
            });
        }

        return new Dictionary<string, object?>
        {
            ["collisionType"] = node.GetInt("collisiontype"),
            ["spheres"] = spheres,
        };
    }

    private static Dictionary<string, float> ToVec3(float[] values, int offset = 0, int? enforceLength = null)
    {
        if (enforceLength.HasValue && values.Length != enforceLength.Value)
            throw new MotionParseException(
                $"expected {enforceLength.Value} components, got {values.Length} (RaceMotionData.cpp:334)");
        return new Dictionary<string, float>
        {
            ["x"] = values[offset],
            ["y"] = values[offset + 1],
            ["z"] = values[offset + 2],
        };
    }
}
