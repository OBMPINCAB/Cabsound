using System.Text.RegularExpressions;

namespace CabSound.Core;

/// <summary>What the scanner needs to know about one sound of a table.</summary>
public sealed record SoundInfo(string Name, SoundOutput Output, int Channels, int Bits, bool IsWav, AudioFacts? Audio = null);

public enum FindingKind
{
    OppositePhase,       // stereo table sound whose L and R cancel: VPX plays L+R, so it is weak (stereo is fine otherwise)
    SilentAudio,         // the sound's audio is all zeros or below -60 dB
    RollMissing,         // a ball rolling sound the script plays but the table doesn't have (silent)
    RollUnplaced,        // a ball rolling sound played without a front/rear position (all exciters)
    RollClips,           // stereo rolling sound whose L+R goes past full scale (VPX sums L+R)
    MissingSound,        // the script plays a sound the table doesn't have (silent)
    HitVolumeAdded,      // Vol(ball)+X: every hit at full volume; should be Vol(ball)*X
    UnplacedSound,       // table sound played without a front/rear position (all exciters equally)
    NoFrontRear,         // no PlaySound in the script uses a fade: nothing is placed front/rear (pre-SSF script)
    KnockerOnBackglass,  // knocker sound goes to the backbox, not the exciters/shaker
    LoudRoll,            // rolling volume multiplied by 5 or more: clips
    DofOnlyShaker,       // shaker motor sub fires DOF only, playing no audio for SSF bass shaker
}

/// <summary>Line = 1-based script line (0 = about the sound itself); Code = that line without its comment.</summary>
public sealed record SoundFinding(FindingKind Kind, string Sound, int Line = 0, string Code = "");

/// <summary>Sound checks for the SSF cabinet, from a table's script and sound list. Pure text checks: they flag, a person decides.</summary>
public static class TableSoundCheck
{
    private const RegexOptions Ci = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    // A call of something named *Sound*/*Snd* whose first argument is a string literal ("x" or ""x"" inside a timer string).
    private static readonly Regex SoundRef = new("""\b(?<fn>\w*(?:sound|snd)\w*)\s*\(?\s*(?<q>"{1,2})(?<name>[^",\s][^"]*)\k<q>(?<amp>\s*&)?""", Ci);
    // Played on purpose without a position: cabinet-level sounds (the shaker, nudges, coins, start button, tilt).
    private static readonly Regex CabinetSound = new("""nudge|coin|start_?button|shaker|tilt""", Ci);
    private static readonly Regex OuterPlay = new("""\b(?<fn>Play\w*Sound\w*|PlaySound\w*)\s*\(?\s*$""", Ci);
    private static readonly Regex SubStart = new("""^\s*(?:Public\s+|Private\s+)?(?:Sub|Function)\s+(?<name>\w+)""", Ci);
    private static readonly Regex SubEnd = new("""^\s*End\s+(?:Sub|Function)\b""", Ci);
    // The same call with a name held in a string Const (Const SFlipperOn = "FlipperUp"): fn only, the name is a lookahead
    // so "PlaySound SoundFX(SFlipperOn" yields both PlaySound and SoundFX.
    private static readonly Regex ConstRef = new("""\b(?<fn>\w*(?:sound|snd)\w*)(?=\s*\(?\s*(?<id>[A-Za-z_]\w*)\b(?!\s*&))""", Ci);
    private static readonly Regex ConstString = new("""^\s*(?:Public\s+|Private\s+)?Const\s+(?<id>\w+)\s*=\s*"(?<value>[^"]+)"\s*$""", Ci);
    private static readonly Regex OneLineEnd = new("""\bEnd\s+(?:Sub|Function)\s*$""", Ci);
    private static readonly Regex HitAdded = new("""\bVol\s*\([^()]*\)\s*\+\s*\w""", Ci);
    private static readonly Regex RollName = new("""(?<!sc|t)roll(?!over)""", Ci);
    private static readonly Regex VolTimes = new("""\bVol\s*\((?:[^()]|\([^()]*\))*\)\s*\*\s*(?<k>\d+(?:\.\d+)?)""", Ci);

    public static IReadOnlyList<SoundFinding> Run(string script, IReadOnlyList<SoundInfo> sounds)
    {
        var findings = new List<SoundFinding>();
        foreach (var s in sounds.Where(s => s.Audio is not null))
        {
            var table = s.Output == SoundOutput.Table;
            if (table && s.Audio!.Correlation <= -0.8) findings.Add(new(FindingKind.OppositePhase, s.Name));
            if (s.Audio!.PeakDb < -60) findings.Add(new(FindingKind.SilentAudio, s.Name));
            if (table && s.Audio.SumClips && IsRoll(s.Name)) findings.Add(new(FindingKind.RollClips, s.Name));
        }

        var byName = new Dictionary<string, SoundInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sounds) byName.TryAdd(s.Name, s);
        var lines = JoinContinuations(CodeLines(script));
        var placingSubs = PlacingSubs(lines);
        var dead = DeadLines(lines);
        var consts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in lines)
            if (ConstString.Match(l) is { Success: true } c) consts.TryAdd(c.Groups["id"].Value, c.Groups["value"].Value);

        for (var i = 0; i < lines.Length; i++)
        {
            var code = lines[i];
            if (code.Length == 0 || dead[i]) continue;
            void Add(FindingKind k, string sound = "") => findings.Add(new(k, sound, i + 1, code.Trim()));

            if (HitAdded.IsMatch(code)) Add(FindingKind.HitVolumeAdded);
            if (VolTimes.Matches(code).Any(m => double.Parse(m.Groups["k"].Value, System.Globalization.CultureInfo.InvariantCulture) >= 5))
                Add(FindingKind.LoudRoll);

            var refs = SoundRef.Matches(code).Select(m => (m.Index, Fn: m.Groups["fn"].Value, Name: m.Groups["name"].Value, Built: m.Groups["amp"].Success))
                .Concat(ConstRef.Matches(code).Where(m => consts.ContainsKey(m.Groups["id"].Value))
                    .Select(m => (m.Index, Fn: m.Groups["fn"].Value, Name: consts[m.Groups["id"].Value], Built: false)))
                .OrderBy(r => r.Index);
            foreach (var m in refs)
            {
                var fn = m.Fn;
                if (fn.Equals("StopSound", StringComparison.OrdinalIgnoreCase)) continue;
                var name = m.Name;
                var built = m.Built;
                var sound = built ? sounds.FirstOrDefault(s => s.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                                  : byName.GetValueOrDefault(name);
                if (sound is null) { Add(IsRoll(name) ? FindingKind.RollMissing : FindingKind.MissingSound, built ? name + "*" : name); continue; }

                if (sound.Output == SoundOutput.Backglass && name.Contains("knock", StringComparison.OrdinalIgnoreCase))
                    Add(FindingKind.KnockerOnBackglass, sound.Name);

                if (fn.StartsWith("SoundFX", StringComparison.OrdinalIgnoreCase))
                {
                    var outer = OuterPlay.Match(code[..m.Index]);
                    if (!outer.Success) continue;
                    fn = outer.Groups["fn"].Value;
                }
                else if (!fn.StartsWith("Play", StringComparison.OrdinalIgnoreCase)) continue;

                var placed = code[m.Index..].Contains("fade", StringComparison.OrdinalIgnoreCase) || placingSubs.Contains(fn);
                if (!placed && sound.Output == SoundOutput.Table && !CabinetSound.IsMatch(sound.Name))
                    Add(IsRoll(sound.Name) ? FindingKind.RollUnplaced : FindingKind.UnplacedSound, sound.Name);
            }
        }

        var anyPlaySound = lines.Any(l => l.Contains("PlaySound", StringComparison.OrdinalIgnoreCase));
        var anyFade = lines.Any(l => l.Contains("PlaySound", StringComparison.OrdinalIgnoreCase) && l.Contains("Fade", StringComparison.OrdinalIgnoreCase));
        if (anyPlaySound && !anyFade) findings.Add(new(FindingKind.NoFrontRear, ""));

        foreach (var (name, start, end) in SubBlocks(lines))
        {
            if (dead[start] || !name.Contains("shake", StringComparison.OrdinalIgnoreCase)) continue;
            var body = string.Join("\n", lines[start..(end + 1)]);
            var hasDof = Regex.IsMatch(body, @"\bDOF(?:ALT)?\b", Ci);
            var hasSound = Regex.IsMatch(body, @"\b(?:Play\w*Sound\w*|SoundFX\w*|\w*Snd\w*)\b", Ci);
            if (hasDof && !hasSound)
                findings.Add(new(FindingKind.DofOnlyShaker, name, start + 1, lines[start].Trim()));
        }

        return findings;
    }

    /// <summary>A ball rolling sound (BallRoll_*, fx_ballrolling, ramp/wire/plastic rolls): "roll" in the name, but not
    /// rollover (a switch), troll or scroll.</summary>
    private static bool IsRoll(string name) => RollName.IsMatch(name);

    /// <summary>Script lines with comments removed (a ' outside a string starts a comment; "" inside a string toggles twice).</summary>
    public static string[] CodeLines(string script) =>
        script.Split('\n').Select(l => StripComment(l.TrimEnd('\r'))).ToArray();

    /// <summary>A line ending in " _" continues on the next: the joined statement stays on the first line number, the rest become empty.</summary>
    private static string[] JoinContinuations(string[] lines)
    {
        var result = (string[])lines.Clone();
        for (var i = 0; i < result.Length; i++)
        {
            var j = i;
            while (result[i].EndsWith('_') && j + 1 < result.Length)
            {
                result[i] = result[i][..^1] + " " + result[++j].Trim();
                result[j] = "";
            }
            i = j;
        }
        return result;
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inString = !inString;
            else if (line[i] == '\'' && !inString) return line[..i].TrimEnd();
        }
        if (line.TrimStart().StartsWith("Rem ", StringComparison.OrdinalIgnoreCase)) return "";
        return line;
    }

    /// <summary>Sub/Function blocks: name, first and last line (0-based, inclusive). "Sub X: ...: End Sub" on one line counts.</summary>
    private static List<(string Name, int Start, int End)> SubBlocks(string[] lines)
    {
        var blocks = new List<(string, int, int)>();
        string? current = null;
        var from = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var start = SubStart.Match(lines[i]);
            if (start.Success)
            {
                current = start.Groups["name"].Value;
                from = i;
                if (OneLineEnd.IsMatch(lines[i])) { blocks.Add((current, i, i)); current = null; }
            }
            else if (current is not null && SubEnd.IsMatch(lines[i])) { blocks.Add((current, from, i)); current = null; }
        }
        return blocks;
    }

    /// <summary>Lines inside every definition of a Sub/Function except its last one (VBScript runs the last).</summary>
    private static bool[] DeadLines(string[] lines)
    {
        var dead = new bool[lines.Length];
        foreach (var group in SubBlocks(lines).GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
            foreach (var (_, start, end) in group.SkipLast(1))
                for (var i = start; i <= end; i++) dead[i] = true;
        return dead;
    }

    /// <summary>Subs/Functions that place their sound front/rear: the body uses a fade, or calls another sub that does.</summary>
    private static HashSet<string> PlacingSubs(string[] lines)
    {
        var bodies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, start, end) in SubBlocks(lines))
            bodies[name] = string.Join("\n", lines[start..(end + 1)]); // the last definition wins, as in VBScript

        var result = new HashSet<string>(bodies.Where(b => b.Value.Contains("Fade", StringComparison.OrdinalIgnoreCase)).Select(b => b.Key),
            StringComparer.OrdinalIgnoreCase);
        for (var grew = true; grew;)
        {
            var calling = bodies.Keys.Where(k => !result.Contains(k) &&
                result.Any(p => Regex.IsMatch(bodies[k], $@"\b{Regex.Escape(p)}\b", Ci))).ToList();
            result.UnionWith(calling);
            grew = calling.Count > 0;
        }
        return result;
    }
}
