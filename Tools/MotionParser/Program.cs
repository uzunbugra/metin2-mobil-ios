using System.Text.Json;
using Metin2.Tools.MotionParser;

namespace Metin2.Tools.MotionParser
{
    /// <summary>
    /// SP10-6 — motion metadata parser CLI. Parses a PC-client race folder
    /// (motlist.txt + the .msa files it references) into JSON for the Unity
    /// animation import step.
    ///
    /// Format sources (fulldosya/source/Client Source/source/GameLib/):
    ///   - motlist.txt: RaceManager.cpp:180-326
    ///       line format "MODE TYPE FILE [PERCENT]", e.g. "GENERAL WAIT 00.msa 65";
    ///       type → canonical name table at RaceManager.cpp:189-237;
    ///       unknown types retried with up to 2 trailing chars stripped
    ///       (WAIT4 → WAIT, RaceManager.cpp:278-294).
    ///   - .msa: RaceMotionData.cpp:309-472 (ScriptType MotionData;
    ///       MotionFileName / MotionDuration / Accumulation x y z;
    ///       Group ComboInputData | AttackingData | LoopData | MotionEventData).
    ///   - events: RaceMotionDataEvent.h (type ids RaceMotionData.h:159-175;
    ///       EFFECT=1, SCREEN_WAVING=2, SCREEN_FLASHING=3, SPECIAL_ATTACKING=4,
    ///       SOUND=5, FLY=6, CHARACTER_SHOW=7, CHARACTER_HIDE=8, WARP=9,
    ///       EFFECT_TO_TARGET=10). Event frames use the client game FPS 60
    ///       (GameType.cpp:5).
    ///
    /// Usage:
    ///   metin2-motionparser parse &lt;raceRootDir&gt; &lt;out.json&gt;
    ///
    ///     &lt;raceRootDir&gt;  race folder containing motlist.txt and the .msa
    ///                    files it references, e.g.
    ///                    "Extracted/Monster/ymir work/monster/wolf"
    ///                    (PC races use the same layout, e.g.
    ///                    "Extracted/PC/ymir work/pc/warrior" — note that this
    ///                    distribution's PC pack ships no motlist.txt; every
    ///                    pack was scanned via PackExtractor 'list').
    ///     &lt;out.json&gt;     output path.
    ///
    /// Output JSON:
    ///   {
    ///     "race":   "&lt;folder name&gt;",
    ///     "source": "&lt;absolute race root&gt;",
    ///     "stats":  { "motionCount", "msaParsed", "eventCount" },
    ///     "motlist": {
    ///       "&lt;TYPE&gt;": {                    // raw motlist type token, e.g. "WAIT1"
    ///         "mode": "GENERAL",             // parsed but unused by the client
    ///                                       // (RaceManager.cpp:249,305)
    ///         "canonical": "NAME_WAIT",      // resolved motion name
    ///         "resolvedByTruncation": false, // WAIT4→WAIT style fallback
    ///         "percent": 65,                 // selection weight
    ///         "msa": "00.msa",
    ///         "duration": 2.666667,          // MotionDuration (seconds)
    ///         "accumulation": null | { "x", "y", "z" },
    ///         "isLoop"/"loop": null | { loopCount, cancelEnableSkill, startTime, endTime },
    ///         "isCombo"/"combo": null | { preInputTime, directInputTime, inputLimitTime },
    ///         "isAttacking"/"attacking": null | { attackType, hittingType, ...,
    ///                                   hitData: [ { attackingStartTime, ...,
    ///                                     hitPositions: [ { time, lastPosition, position } ] } ] },
    ///         "events": [ { index, type, typeName, frame, startingTime, duringTime,
    ///                       ...type-specific fields (effectFile, soundFile,
    ///                       flyFile, attachingBone, attack, collision, ...) } ]
    ///       }
    ///     }
    ///   }
    ///
    /// Fail-closed: any missing .msa, malformed token, unknown event type or
    /// unbalanced brace aborts with exit code 1 and no output file is written.
    /// CP1254/extended bytes are read as Latin-1 and passed through verbatim
    /// (asset names are ASCII).
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length != 3 || !args[0].Equals("parse", StringComparison.OrdinalIgnoreCase))
            {
                PrintUsage();
                return 2;
            }

            try
            {
                var catalog = MsaParser.ParseRace(args[1]);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(args[2], JsonSerializer.Serialize(catalog, options));

                var stats = (Dictionary<string, object?>)catalog["stats"]!;
                Console.WriteLine(
                    $"race={catalog["race"]} motions={stats["motionCount"]} " +
                    $"msaParsed={stats["msaParsed"]} events={stats["eventCount"]} -> {args[2]}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine(
                "Usage:\n" +
                "  metin2-motionparser parse <raceRootDir> <out.json>\n" +
                "    <raceRootDir>  folder containing motlist.txt + .msa files\n" +
                "    <out.json>     output JSON path");
        }
    }
}
