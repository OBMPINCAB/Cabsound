using System.Text;
using System.Text.RegularExpressions;

namespace CabSound.Core;

/// <summary>One table of a folder scan. NotScannedReason is set when it could not be (or is not) scanned.</summary>
public sealed record TableScan(string File, string? NotScannedReason, IReadOnlyList<SoundFinding> Findings,
    int Sounds, string? Rom, bool Sidecar, string Changed)
{
    public static TableScan Scanned(string file, IReadOnlyList<SoundFinding> findings, int sounds, string? rom, bool sidecar, string changed) =>
        new(file, null, findings, sounds, rom, sidecar, changed);

    public static TableScan NotScanned(string file, string reason, string changed = "") => new(file, reason, [], 0, null, false, changed);

    public bool WasScanned => NotScannedReason is null;
}

/// <summary>Text reports for the table sound scanner: the full table list and one table's details.</summary>
public static class TableSoundReport
{
    private static readonly Regex GameName = new("""cGameName\s*=\s*"(?<rom>[^"]+)""", RegexOptions.IgnoreCase);
    private static readonly Regex Stamp = new(@"\.(?<d>\d{8})-\d+", RegexOptions.CultureInvariant);

    /// <summary>The ROM name when the script runs PinMAME (LoadVPM or Controller.GameName); null for original-code tables.</summary>
    public static string? Rom(string script)
    {
        var runsRom = script.Contains("LoadVPM", StringComparison.OrdinalIgnoreCase) || script.Contains("Controller.GameName", StringComparison.OrdinalIgnoreCase);
        var m = GameName.Match(script);
        return runsRom && m.Success ? m.Groups["rom"].Value : null;
    }

    /// <summary>"CabSound sidecar; backups yyyy-MM-dd, ..." from App\backups file names and the sidecar's first line; "" if none.</summary>
    public static string ChangedBefore(string tableFile, IEnumerable<string> backupNames, string? sidecarText)
    {
        var stem = Path.GetFileNameWithoutExtension(tableFile);
        var dates = backupNames
            .Where(b => b.StartsWith(tableFile + ".", StringComparison.OrdinalIgnoreCase) || b.StartsWith(stem + ".vbs.", StringComparison.OrdinalIgnoreCase))
            .Select(b => Stamp.Match(b))
            .Where(m => m.Success)
            .Select(m => $"{m.Groups["d"].Value[..4]}-{m.Groups["d"].Value[4..6]}-{m.Groups["d"].Value[6..]}")
            .Distinct().Order().ToList();
        var parts = new List<string>();
        if (sidecarText?.StartsWith("' CabSound sidecar", StringComparison.Ordinal) == true) parts.Add("CabSound sidecar");
        if (dates.Count > 0) parts.Add("backups " + string.Join(", ", dates));
        return string.Join("; ", parts);
    }

    private static readonly (FindingKind Kind, string Short, string Title, string Fix)[] Kinds =
    [
        (FindingKind.HitVolumeAdded, "hit-vol", "Hit volume added, not multiplied", "Vol(ball)*VolTable instead of +, so soft hits are quiet (then raise the table volume)."),
        (FindingKind.NoFrontRear, "no front/rear", "No front/rear placement at all", "Add AudioFade to the PlaySound helpers (pre-SSF script)."),
        (FindingKind.OppositePhase, "opp-phase", "Stereo sounds that cancel out in VPX (L+R)", "Check by ear; use one channel instead (by hand)."),
        (FindingKind.SilentAudio, "silent", "Sounds with no audio (below -60 dB)", "Replace with a working sound or comment the call out."),
        (FindingKind.RollMissing, "roll-missing", "Ball rolling sounds the table doesn't have (silent)", "Point at the table's rolling sounds."),
        (FindingKind.RollUnplaced, "roll-unplaced", "Ball rolling played without a position (all exciters)", "Add AudioPan/AudioFade of the ball."),
        (FindingKind.RollClips, "roll-clips", "Stereo rolling sound clips when VPX adds L+R", "Lower its volume in the script."),
        (FindingKind.UnplacedSound, "unplaced", "Table sounds played without a position (all exciters)", "PlaySoundAtVol at the kicker/gate/object instead."),
        (FindingKind.MissingSound, "missing", "Sounds the script plays but the table doesn't have (silent)", "Point at an existing sound, or comment the call out."),
        (FindingKind.KnockerOnBackglass, "knocker-bg", "Knocker plays on the backbox", "Use a Table-output knocker sound placed on the playfield."),
        (FindingKind.LoudRoll, "loud-roll", "Rolling volume multiplied by 5 or more (clips)", "Use about x3."),
        (FindingKind.DofOnlyShaker, "shaker-dof", "Shaker motor sub fires DOF only (no SSF shaker audio)", "Play a Table-output 38 Hz shaker sound in the sub."),
    ];

    public static string Summary(IReadOnlyList<TableScan> tables)
    {
        var scanned = tables.Where(t => t.WasScanned).ToList();
        var ordered = scanned.Where(t => t.Findings.Count > 0).OrderByDescending(t => t.Findings.Count).ThenBy(t => t.File, StringComparer.OrdinalIgnoreCase)
            .Concat(scanned.Where(t => t.Findings.Count == 0).OrderBy(t => t.File, StringComparer.OrdinalIgnoreCase))
            .Concat(tables.Where(t => !t.WasScanned).OrderBy(t => t.File, StringComparer.OrdinalIgnoreCase));

        var sb = new StringBuilder();
        sb.AppendLine($"{tables.Count} tables: {scanned.Count} scanned ({scanned.Count(t => t.Findings.Count > 0)} with issues), {tables.Count - scanned.Count} not scanned");
        sb.AppendLine("table | issues | what | notes");
        foreach (var t in ordered)
        {
            var what = t.WasScanned
                ? string.Join(", ", Kinds.Select(k => (k.Short, n: t.Findings.Count(f => f.Kind == k.Kind))).Where(x => x.n > 0)
                    .Select(x => x.Short == "no front/rear" ? x.Short : $"{x.Short} {x.n}"))
                : "NOT SCANNED: " + t.NotScannedReason;
            sb.AppendLine($"{t.File} | {(t.WasScanned ? t.Findings.Count.ToString() : "-")} | {(what.Length == 0 ? "ok" : what)} | {Notes(t)}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string Notes(TableScan t) => string.Join("; ", new[]
    {
        t.Rom is null ? null : "ROM " + t.Rom,
        t.Sidecar ? "sidecar .vbs" : null,
        t.Changed.Length == 0 ? null : "changed before: " + t.Changed,
    }.Where(s => s is not null));

    public static string Detail(TableScan t)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {t.File}");
        if (!t.WasScanned) { sb.AppendLine("NOT SCANNED: " + t.NotScannedReason); return sb.ToString().TrimEnd(); }
        sb.AppendLine($"{t.Sounds} sounds; script: {(t.Sidecar ? "sidecar .vbs (VPX runs this one)" : "inside the .vpx")}" +
                      (t.Rom is null ? "; original code (no ROM)" : $"; ROM: {t.Rom}") +
                      (t.Changed.Length == 0 ? "" : $"; changed before: {t.Changed}"));
        if (t.Findings.Count == 0) { sb.AppendLine("Nothing to do."); return sb.ToString().TrimEnd(); }
        foreach (var (kind, _, title, fix) in Kinds)
        {
            var f = t.Findings.Where(x => x.Kind == kind).ToList();
            if (f.Count == 0) continue;
            if (kind is FindingKind.OppositePhase or FindingKind.SilentAudio or FindingKind.RollClips)
                sb.AppendLine($"- {title} ({f.Count}): {string.Join(", ", f.Select(x => x.Sound))}");
            else
            {
                sb.AppendLine($"- {title} ({f.Count}):");
                // The sound's name: a line can play two sounds (e.g. InitExitSnd), only one of them flagged.
                foreach (var x in f.Where(x => x.Line > 0))
                    sb.AppendLine(x.Sound.Length == 0 ? $"    line {x.Line}: {x.Code}" : $"    line {x.Line} (\"{x.Sound}\"): {x.Code}");
            }
            sb.AppendLine($"  Fix: {fix}");
        }
        return sb.ToString().TrimEnd();
    }
}
