using System.Diagnostics;
using CabSound.Core;

// CabSound TableCheck: sound/SSF checks for VPX tables.
//   scan [folder ...]      every table in the folders (default: VPX + Future Pinball tables): full list + details file. Changes nothing.
//   check <table.vpx>      one table's details. Changes nothing.
//   stereo [table.vpx]     what the 2026-11-13 mono step changed and what unmono would undo (all tables: docs\tables-stereo-report.md). Changes nothing.
//   unmono <table.vpx>     give those sounds their stereo back from the backups (per sound the first whose mono matches). Backup first; patched copy verified (streams + VPX loads it) before it replaces the table.
//   backglass <table.vpx> <name|prefix*> ...   those Table-output sounds -> Backglass output (voices/callouts/music to the backbox). Same safety steps.
//   table <table.vpx> <name|prefix*> ...       those Backglass-output sounds -> Table output (impact effects back on the exciters + shaker;
//                                              the backbox does not drive the sub/shaker). Same safety steps.
// Options: --backups <dir> (default App\backups), --vpx <VPinballX64.exe>.
const string DefaultTables = @"C:\vPinball\VisualPinball\Tables";
const string DefaultFpTables = @"C:\vPinball\FuturePinball\Tables";
const string DefaultBackups = @"C:\vPinball\CabSound\App\backups";
const string DefaultVpx = @"C:\vPinball\VisualPinball\VPinballX64.exe";

var options = new Dictionary<string, string>();
var rest = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (args[i].StartsWith("--") && i + 1 < args.Length) options[args[i][2..]] = args[++i];
    else rest.Add(args[i]);
}
var backups = options.GetValueOrDefault("backups", DefaultBackups);
var vpx = options.GetValueOrDefault("vpx", DefaultVpx);

try
{
    return rest switch
    {
        ["scan", .. var folders] => Scan(folders.Count > 0 ? folders : [DefaultTables, DefaultFpTables]),
        ["check", var table] => Check(table),
        ["stereo"] => StereoAll(),
        ["stereo", var table] => StereoOne(table),
        ["unmono", var table] => Unmono(table),
        ["backglass", var table, .. var patterns] when patterns.Count > 0 => MoveOutput(table, patterns, SoundOutput.Backglass),
        ["table", var table, .. var patterns] when patterns.Count > 0 => MoveOutput(table, patterns, SoundOutput.Table),
        ["shaker", var table, .. var patterns] when patterns.Count > 0 => SynthesizeShaker(table, patterns),
        ["restore", var table, var backupVpx, .. var patterns] when patterns.Count > 0 => Restore(table, backupVpx, patterns),
        _ => Usage(),
    };
}
catch (Exception e)
{
    Console.WriteLine("FAILED: " + e.Message);
    return 1;
}

int Usage()
{
    Console.WriteLine("usage: scan [folder ...] | check <table.vpx> | stereo [table.vpx] | unmono <table.vpx> | backglass <table.vpx> <name|prefix*> ... | table <table.vpx> <name|prefix*> ... | shaker <table.vpx> <name|prefix*> ... [--seconds S] | restore <table.vpx> <backup.vpx> <name|prefix*> ...   [--backups dir] [--vpx exe]");
    return 2;
}

TableScan ScanOne(string path)
{
    var file = Path.GetFileName(path);
    var backupNames = Directory.Exists(backups) ? Directory.GetFiles(backups).Select(Path.GetFileName).OfType<string>().ToList() : [];
    var sidecar = VpxFile.SidecarPath(path);
    var sidecarText = sidecar is null ? null : VpxFile.ReadText(sidecar);
    var changed = TableSoundReport.ChangedBefore(file, backupNames, sidecarText);
    if (!path.EndsWith(".vpx", StringComparison.OrdinalIgnoreCase)) return TableScan.NotScanned(file, "Future Pinball table (not handled)", changed);
    try
    {
        var vpxFile = VpxFile.Read(path);
        var script = sidecarText ?? vpxFile.EmbeddedScript;
        if (script is null) return TableScan.NotScanned(file, "script not readable (protected table?)", changed);
        var findings = TableSoundCheck.Run(script, vpxFile.Sounds.Select(s => s.Sound.Info).ToList());
        return TableScan.Scanned(file, findings, vpxFile.Sounds.Count, TableSoundReport.Rom(script), sidecar is not null, changed);
    }
    catch (Exception e)
    {
        return TableScan.NotScanned(file, "could not read: " + e.Message, changed);
    }
}

int Scan(IReadOnlyList<string> folders)
{
    var files = folders.Where(Directory.Exists)
        .SelectMany(f => Directory.EnumerateFiles(f).Where(p => p.EndsWith(".vpx", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".fpt", StringComparison.OrdinalIgnoreCase)))
        .Order(StringComparer.OrdinalIgnoreCase).ToList();
    foreach (var f in folders.Where(f => !Directory.Exists(f))) Console.WriteLine($"(folder not found: {f})");
    var results = new List<TableScan>();
    foreach (var (f, n) in files.Select((f, n) => (f, n + 1)))
    {
        Console.Error.Write($"\r{n}/{files.Count} {Path.GetFileName(f),-70}");
        results.Add(ScanOne(f));
    }
    Console.Error.WriteLine();
    var summary = TableSoundReport.Summary(results);
    var report = Path.Combine(Environment.CurrentDirectory, $"table-check-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
    File.WriteAllText(report, summary + Environment.NewLine + Environment.NewLine +
        string.Join(Environment.NewLine + Environment.NewLine, results.OrderBy(r => r.File, StringComparer.OrdinalIgnoreCase).Select(TableSoundReport.Detail)));
    Console.WriteLine(summary);
    Console.WriteLine();
    Console.WriteLine("Full report: " + report);
    return 0;
}

int Check(string table)
{
    Console.WriteLine(TableSoundReport.Detail(ScanOne(Path.GetFullPath(table))));
    return 0;
}

// What the mono step changed in one table, against its backups, oldest first (sounds = the table's sounds now).
// Before: backup file -> stream -> sound, for unmono.
(TableStereo Report, Dictionary<string, Dictionary<string, VpxSound>> Before) StereoOf(string path, IReadOnlyList<(string Stream, VpxSound Sound)> sounds)
{
    var file = Path.GetFileName(path);
    var backupNames = Directory.Exists(backups) ? Directory.GetFiles(backups).Select(Path.GetFileName).OfType<string>() : [];
    var names = StereoRestore.Backups(file, backupNames);
    if (names.Count == 0) return (new TableStereo(file, null, []), []);
    var old = names.Select(n => (Backup: n, Sounds: VpxFile.Read(Path.Combine(backups, n)).Sounds)).ToList();
    var items = StereoRestore.PlanAll(sounds, old.Select(o => (o.Backup, o.Sounds)).ToList());
    return (new TableStereo(file, names[0], items), old.ToDictionary(o => o.Backup, o => o.Sounds.ToDictionary(s => s.Stream, s => s.Sound)));
}

int StereoAll()
{
    var tables = Directory.EnumerateFiles(DefaultTables, "*.vpx").Order(StringComparer.OrdinalIgnoreCase).ToList();
    var results = new List<TableStereo>();
    foreach (var (f, n) in tables.Select((f, n) => (f, n + 1)))
    {
        Console.Error.Write($"\r{n}/{tables.Count} {Path.GetFileName(f),-70}");
        try { results.Add(StereoOf(f, VpxFile.Read(f).Sounds).Report); }
        catch (Exception e) { results.Add(new TableStereo(Path.GetFileName(f), null, [], "could not read: " + e.Message)); }
    }
    Console.Error.WriteLine();
    var report = Path.Combine(Environment.CurrentDirectory, "docs", "tables-stereo-report.md");
    File.WriteAllText(report, StereoRestore.Markdown(results));
    var restore = results.Where(r => r.Items.Any(i => i.Verdict == RestoreVerdict.Restore)).ToList();
    Console.WriteLine($"{results.Count} tables: {restore.Count} with sounds to restore ({restore.Sum(r => r.Items.Count(i => i.Verdict == RestoreVerdict.Restore))} sounds).");
    Console.WriteLine("Report: " + report);
    return 0;
}

int StereoOne(string table)
{
    table = Path.GetFullPath(table);
    Console.WriteLine(StereoRestore.Detail(StereoOf(table, VpxFile.Read(table).Sounds).Report));
    return 0;
}

// Undo the mono step: the sounds it changed get the audio of the backup they match back; output (e.g. backbox) stays.
int Unmono(string table)
{
    table = Path.GetFullPath(table);
    var contents = ReadClosed(table);
    var (report, before) = StereoOf(table, contents.Sounds);
    if (report.Backup is null) { Console.WriteLine("Never made mono by CabSound: nothing to do."); return 0; }
    var now = contents.Sounds.ToDictionary(s => s.Stream, s => s.Sound);
    var replace = new Dictionary<string, VpxSound>();
    foreach (var item in report.Items)
    {
        if (item.Verdict == RestoreVerdict.LeftAlone) { Console.WriteLine($"Left alone: {item.Name} ({item.Reason})"); continue; }
        replace[item.Stream] = now[item.Stream].WithAudioFrom(before[item.Source][item.Stream]);
        Console.WriteLine($"{item.Name}: mono -> stereo from {item.Source} ({item.Output} output kept)");
    }
    if (replace.Count == 0) { Console.WriteLine("Nothing left to restore."); return 0; }
    return Patch(table, contents, replace, "given their stereo back");
}

// Table output = exciters + shaker (impact effects); Backglass output = backbox, which does not drive the sub/shaker
// (voices, callouts, music). Same safe patch path both ways.
int MoveOutput(string table, IReadOnlyList<string> patterns, SoundOutput to)
{
    table = Path.GetFullPath(table);
    var contents = ReadClosed(table);
    var from = to == SoundOutput.Table ? SoundOutput.Backglass : SoundOutput.Table;
    var replace = new Dictionary<string, VpxSound>();
    foreach (var (stream, sound) in SoundOutputMove.Plan(contents.Sounds, patterns, to))
    {
        replace[stream] = sound;
        Console.WriteLine($"{sound.Name}: {from} -> {to}");
    }
    foreach (var p in SoundOutputMove.Unmatched(contents.Sounds, patterns))
        Console.WriteLine($"(no sound matches {p})");
    if (replace.Count == 0) { Console.WriteLine($"No {from}-output sound matches: nothing to do."); return 0; }
    return Patch(table, contents, replace, to == SoundOutput.Table ? "now on the table (exciters + shaker)" : "now on the backbox");
}

// Adds 38 Hz SSF shaker audio: mixes a 38 Hz underlay into audible toy sounds (default) or replaces silent/unused slots (--mode replace).
int SynthesizeShaker(string table, IReadOnlyList<string> patterns)
{
    table = Path.GetFullPath(table);
    var mode = options.GetValueOrDefault("mode", "auto");
    double? customSeconds = options.TryGetValue("seconds", out var secStr)
        ? double.Parse(secStr, System.Globalization.CultureInfo.InvariantCulture)
        : null;
    var contents = ReadClosed(table);
    var replace = new Dictionary<string, VpxSound>();
    foreach (var (stream, sound) in contents.Sounds.Where(s => VpxSound.NameMatches(s.Sound.Name, patterns)))
    {
        var isSilent = sound.Info.Audio is { PeakDb: < -60 };
        var doReplace = mode.Equals("replace", StringComparison.OrdinalIgnoreCase) || (mode.Equals("auto", StringComparison.OrdinalIgnoreCase) && isSilent);
        var patched = doReplace ? sound.WithShakerPulse(seconds: customSeconds) : sound.WithShakerUnderlay();
        replace[stream] = patched;
        Console.WriteLine($"{sound.Name}: {(doReplace ? "replaced with" : "mixed")} 38 Hz SSF shaker ({patched.DurationSeconds:0.00} s, Table output)");
    }
    foreach (var p in SoundOutputMove.Unmatched(contents.Sounds, patterns))
        Console.WriteLine($"(no sound matches {p})");
    if (replace.Count == 0) { Console.WriteLine("No matching sound streams: nothing to do."); return 0; }
    return Patch(table, contents, replace, "given 38 Hz SSF shaker audio");
}

// Undo a mono step for some sounds: their audio comes back from a backup .vpx; output (e.g. backbox) stays as it is now.
int Restore(string table, string backupVpx, IReadOnlyList<string> patterns)
{
    table = Path.GetFullPath(table);
    var contents = ReadClosed(table);
    var old = VpxFile.Read(Path.GetFullPath(backupVpx)).Sounds.ToDictionary(s => s.Stream, s => s.Sound);
    var replace = new Dictionary<string, VpxSound>();
    foreach (var (stream, sound) in contents.Sounds.Where(s => VpxSound.NameMatches(s.Sound.Name, patterns)))
    {
        if (!old.TryGetValue(stream, out var before) || !before.Name.Equals(sound.Name, StringComparison.OrdinalIgnoreCase))
        { Console.WriteLine($"{sound.Name}: not in the backup at {stream}, skipped"); continue; }
        if (before.Channels == sound.Channels && before.Data.SequenceEqual(sound.Data)) continue;
        replace[stream] = sound.WithAudioFrom(before);
        Console.WriteLine($"{sound.Name}: {sound.Channels} ch -> {before.Channels} ch from backup ({sound.Output} output kept)");
    }
    if (replace.Count == 0) { Console.WriteLine("Nothing differs from the backup: nothing to do."); return 0; }
    return Patch(table, contents, replace, "restored from the backup");
}

VpxFile.Contents ReadClosed(string table)
{
    var running = Process.GetProcesses().Select(p => p.ProcessName)
        .Where(n => n.StartsWith("VPinballX", StringComparison.OrdinalIgnoreCase) || n.StartsWith("PinUp", StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
    if (running.Count > 0) throw new InvalidOperationException("close these first: " + string.Join(", ", running));
    return VpxFile.Read(table);
}

// Patched copy in %TEMP%; only the given sound streams may change; VPX must open it; then backup + replace.
int Patch(string table, VpxFile.Contents contents, Dictionary<string, VpxSound> replace, string what)
{
    var work = Path.Combine(Path.GetTempPath(), "CabSound.TableCheck", Path.GetFileNameWithoutExtension(table));
    if (Directory.Exists(work)) Directory.Delete(work, true);
    Directory.CreateDirectory(work);
    var patched = Path.Combine(work, Path.GetFileName(table));
    try
    {
        VpxFile.WriteWithSounds(table, patched, replace);

        var problems = VpxPatchCheck.Problems(VpxFile.StreamHashes(table), VpxFile.StreamHashes(patched), replace.Keys.ToList());
        if (problems.Count > 0) throw new InvalidOperationException("patched copy differs unexpectedly: " + string.Join("; ", problems));
        Console.WriteLine($"Streams: only the {replace.Count} sound streams changed.");

        VerifyWithVpx(patched, contents.EmbeddedScript);
        Console.WriteLine("VPX opens the patched copy (no hash error, script unchanged).");

        var backup = SafeFileWriter.Backup(table, backups, DateTime.Now);
        if (VpxFile.FileHash(backup) != VpxFile.FileHash(table)) throw new IOException("backup copy does not match the table");
        Console.WriteLine("Backup: " + backup);

        var hash = VpxFile.FileHash(patched);
        File.Copy(patched, table, overwrite: true);
        if (VpxFile.FileHash(table) != hash) throw new IOException($"table copy does not match the patched copy; restore from {backup}");
        Console.WriteLine($"Done: {replace.Count} sounds {what} in {Path.GetFileName(table)}.");
        return 0;
    }
    finally { Directory.Delete(work, true); }
}

// VPX -ExtractVBS writes <table>.vbs and vpinball.log into the working folder: run it on the copy, in the work folder only.
void VerifyWithVpx(string patched, string? expectedScript)
{
    var dir = Path.GetDirectoryName(patched)!;
    using var p = Process.Start(new ProcessStartInfo(vpx, ["-ExtractVBS", patched]) { WorkingDirectory = dir, UseShellExecute = false })
        ?? throw new InvalidOperationException("could not start VPX");
    if (!p.WaitForExit(TimeSpan.FromMinutes(3))) { p.Kill(); throw new TimeoutException("VPX -ExtractVBS did not finish"); }
    var log = Path.Combine(dir, "vpinball.log");
    if (File.Exists(log) && File.ReadAllText(log).Contains("hash", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("VPX reports a table hash problem; see " + log);
    var vbs = Path.ChangeExtension(patched, ".vbs");
    if (!File.Exists(vbs)) throw new InvalidOperationException("VPX did not extract the script from the patched copy");
    if (expectedScript is not null && VpxFile.ReadText(vbs) != expectedScript) throw new InvalidOperationException("extracted script differs from the original");
}
