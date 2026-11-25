using System.Text;
using System.Text.RegularExpressions;

namespace CabSound.Core;

public enum RestoreVerdict { Restore, LeftAlone }

/// <summary>One sound the 2026-11-13 mono step changed. Before = facts of the backup's stereo audio; Reason "" for Restore;
/// Source = the backup file the stereo comes from (PlanAll).</summary>
public sealed record RestoreItem(string Stream, string Name, SoundOutput Output, RestoreVerdict Verdict, string Reason, AudioFacts? Before, string Source = "");

/// <summary>One table: its oldest backup (null = never changed by CabSound) and what to restore. NotReason = could not read.</summary>
public sealed record TableStereo(string File, string? Backup, IReadOnlyList<RestoreItem> Items, string? NotReason = null);

/// <summary>
/// Undoing the mono step: VPX 10.8 places stereo table sounds exactly like mono (Windows sums L+R), so the mono step only
/// made those sounds ~6 dB quieter. A sound counts as made mono by CabSound only if its audio is byte-equal to MakeMono()
/// of the same sound in the table's oldest backup.
/// </summary>
public static class StereoRestore
{
    private static readonly Regex Stamp = new(@"^\.(?<s>\d{8}-\d{6})", RegexOptions.CultureInvariant);

    /// <summary>The oldest "&lt;tableFile&gt;.yyyyMMdd-HHmmss....bak" in the list, or null.</summary>
    public static string? OldestBackup(string tableFile, IEnumerable<string> backupNames) =>
        Backups(tableFile, backupNames).FirstOrDefault();

    public static IReadOnlyList<RestoreItem> Plan(IReadOnlyList<(string Stream, VpxSound Sound)> now, IReadOnlyList<(string Stream, VpxSound Sound)> backup)
    {
        var current = now.ToDictionary(s => s.Stream, s => s.Sound, StringComparer.OrdinalIgnoreCase);
        var items = new List<RestoreItem>();
        foreach (var (stream, before) in backup.Where(b => b.Sound.CanMakeMono))
        {
            RestoreItem Item(VpxSound s, RestoreVerdict v, string reason) => new(stream, before.Name, s.Output, v, reason, SoundAudio.Facts(before));

            if (!current.TryGetValue(stream, out var sound) || !sound.Name.Equals(before.Name, StringComparison.OrdinalIgnoreCase))
            { items.Add(Item(before, RestoreVerdict.LeftAlone, "not at the same place in the table")); continue; }
            if (sound.Channels != 1) continue; // still (or again) stereo: nothing of ours to undo
            items.Add(sound.Data.AsSpan().SequenceEqual(before.MakeMono().Mono.Data)
                ? Item(sound, RestoreVerdict.Restore, "")
                : Item(sound, RestoreVerdict.LeftAlone, "not what the mono step made: changed another way, before or since"));
        }
        return items;
    }

    /// <summary>Plan against every backup, oldest first: per sound the first backup it matches wins (a sound changed by
    /// hand on another day before the mono step matches a later backup); with no match, the first "left alone" stays.</summary>
    public static IReadOnlyList<RestoreItem> PlanAll(IReadOnlyList<(string Stream, VpxSound Sound)> now,
        IReadOnlyList<(string Backup, IReadOnlyList<(string Stream, VpxSound Sound)> Sounds)> backupsOldestFirst)
    {
        var byStream = new Dictionary<string, RestoreItem>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var (name, sounds) in backupsOldestFirst)
            foreach (var item in Plan(now, sounds).Select(i => i with { Source = name }))
            {
                if (!byStream.TryGetValue(item.Stream, out var had)) { byStream[item.Stream] = item; order.Add(item.Stream); }
                else if (had.Verdict == RestoreVerdict.LeftAlone && item.Verdict == RestoreVerdict.Restore) byStream[item.Stream] = item;
            }
        return order.Select(s => byStream[s]).ToList();
    }

    /// <summary>Every "&lt;tableFile&gt;.yyyyMMdd-HHmmss....bak" in the list, oldest first.</summary>
    public static IReadOnlyList<string> Backups(string tableFile, IEnumerable<string> backupNames) => backupNames
        .Where(b => b.StartsWith(tableFile, StringComparison.OrdinalIgnoreCase) && b.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        .Select(b => (Name: b, M: Stamp.Match(b[tableFile.Length..])))
        .Where(x => x.M.Success)
        .OrderBy(x => x.M.Groups["s"].Value, StringComparer.Ordinal)
        .Select(x => x.Name)
        .ToList();

    public static string Markdown(IReadOnlyList<TableStereo> tables, DateOnly? date = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Table stereo report ({date ?? DateOnly.FromDateTime(DateTime.Now):yyyy-MM-dd})");
        sb.AppendLine();
        sb.AppendLine("VPX 10.8 places stereo table sounds exactly like mono (measured 2026-11-14); the 2026-11-13 mono step only made");
        sb.AppendLine("them about 6 dB quieter. `table-check.bat unmono \"<table>\"` gives them their stereo back (owner's OK per table).");
        var ordered = tables.OrderBy(t => t.File, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var t in ordered.Where(t => t.Items.Count > 0))
        {
            sb.AppendLine();
            sb.Append(Section(t));
        }
        var rest = ordered.Where(t => t.Items.Count == 0).ToList();
        if (rest.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Nothing to restore");
            sb.AppendLine();
            foreach (var t in rest) sb.AppendLine($"- {t.File}: {Nothing(t)}");
        }
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    /// <summary>One table, for the console.</summary>
    public static string Detail(TableStereo t) => t.Items.Count == 0 ? $"=== {t.File}\n{Nothing(t)}" : Section(t).TrimEnd();

    private static string Nothing(TableStereo t) =>
        t.NotReason ?? (t.Backup is null ? "never made mono by CabSound: nothing to restore" : "nothing left to restore");

    private static string Section(TableStereo t)
    {
        var sb = new StringBuilder();
        var restore = t.Items.Where(i => i.Verdict == RestoreVerdict.Restore).ToList();
        string Names(IEnumerable<RestoreItem> items) => string.Join(", ", items.Select(i => i.Name));
        sb.AppendLine($"## {t.File}");
        sb.AppendLine();
        var sources = restore.Select(i => i.Source).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        sb.AppendLine(sources.Count > 0 ? "Stereo from: " + string.Join(", ", sources.Select(s => $"`{s}`")) : $"Backup: `{t.Backup}`");
        sb.AppendLine();
        if (restore.Count > 0) sb.AppendLine($"- Made mono: {restore.Count} sounds (about 6 dB quieter than before). Restore: {Names(restore)}.");
        foreach (var i in t.Items.Where(i => i.Verdict == RestoreVerdict.LeftAlone)) sb.AppendLine($"- Left alone: {i.Name} ({i.Reason})");
        var opposite = restore.Where(i => i.Before?.Correlation <= -0.8).ToList();
        var clips = restore.Where(i => i.Before?.SumClips == true).ToList();
        var backbox = restore.Where(i => i.Output == SoundOutput.Backglass).ToList();
        if (opposite.Count > 0) sb.AppendLine($"- Opposite phase (weak in VPX, stereo or mono): {Names(opposite)}");
        if (clips.Count > 0) sb.AppendLine($"- Over full scale when VPX adds L+R (it played like this before the mono step; listen): {Names(clips)}");
        if (backbox.Count > 0) sb.AppendLine($"- On the backbox, will get stereo back: {Names(backbox)}");
        return sb.ToString();
    }
}
