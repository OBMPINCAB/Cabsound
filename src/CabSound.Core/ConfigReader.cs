using System.Text.RegularExpressions;

namespace CabSound.Core;

/// <summary>The config.txt CabSound writes: one Include line. Everything else lives in CabSound.txt.</summary>
public static class ManagedConfig
{
    public const string Text =
        "# Managed by CabSound -- do not edit (change sound settings in CabSound)\r\nInclude: " + EqApoFile.FileName + "\r\n";

    /// <summary>True when config.txt is exactly CabSound's (line ends and trailing blank lines don't count).</summary>
    public static bool IsOurs(string config) => Normal(config) == Normal(Text);

    private static string Normal(string text) => text.Replace("\r", "").TrimEnd('\n');
}

/// <summary>
/// What a config.txt found at take-over means for CabSound. Carried = what CabSound keeps doing, in
/// plain words. Dropped = lines CabSound replaces or that did nothing. NotUnderstood = lines that will
/// be lost. Notes = things the owner must know before saying OK. KeepSavedSound = the file already
/// included CabSound.txt (something else changed it), so the sound settings CabSound saved stay as they are.
/// </summary>
public sealed record FoundConfig(
    SoundSettings Sound,
    IReadOnlyList<string> Carried,
    IReadOnlyList<string> Dropped,
    IReadOnlyList<string> NotUnderstood,
    IReadOnlyList<string> Notes,
    bool KeepSavedSound = false);

/// <summary>
/// Reads a config.txt that isn't CabSound's (PinVol's or hand-written) at take-over: finds the low
/// cuts, the 250 Hz dip and a Side/Rear jack swap, and lists every line it can't carry over.
/// Commands are read as Equalizer APO does: any case, any spacing, comments ignored.
/// </summary>
public static partial class ConfigReader
{
    private enum Part { All, Backbox, Front, Rear, Exciters, Sub, Other }

    [GeneratedRegex(@"^on hp fc (\d+) hz$")] private static partial Regex HighPass();
    [GeneratedRegex(@"^on pk fc 250 hz gain -2 db q 1(\.0+)?$")] private static partial Regex Dip();
    [GeneratedRegex(@"\bS[LR]\s*=\s*R[LR]\b", RegexOptions.IgnoreCase)] private static partial Regex SideRearSwap();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();

    public static FoundConfig Read(string config)
    {
        var dropped = new List<string>();
        var notUnderstood = new List<string>();
        var cuts = new Dictionary<Part, List<int>> { [Part.Backbox] = [], [Part.Front] = [], [Part.Rear] = [] };
        var cutLines = new List<string>();
        var dips = new Dictionary<Part, string>();
        var swapped = false;
        var swapAboveLevels = false;   // levels applied after the swap landed on the other exciter pair
        var includesCabSound = false;
        string? swapLine = null;
        var part = Part.All;

        foreach (var raw in config.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            var colon = line.IndexOf(':');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (colon < 0) { notUnderstood.Add(line); continue; }

            var command = line[..colon].Trim().ToLowerInvariant();
            var value = Spaces().Replace(line[(colon + 1)..].Trim(), " ").ToLowerInvariant();
            var wired = part is Part.Backbox or Part.Front or Part.Rear or Part.Exciters;
            switch (command)
            {
                case "channel": part = PartOf(value); break;
                case "stage" when value == "post-mix": break;
                case "device" when value == "all": dropped.Add(line); break;
                case "include" when value is "pinvolssf.txt" or "cabsoundssf.txt" or "cabsound.txt":
                    dropped.Add(line);
                    if (value == "cabsound.txt") includesCabSound = true;
                    else if (swapped) swapAboveLevels = true;
                    break;
                case "preamp" when part is Part.All or Part.Sub: dropped.Add(line); break;
                case "filter" when part == Part.Sub: dropped.Add(line); break;
                case "copy" when SideRearSwap().IsMatch(value): swapped = true; swapLine = line; break;
                case "filter" when wired && HighPass().Match(value) is { Success: true } hp && int.TryParse(hp.Groups[1].Value, out var hz):
                    foreach (var p in Split(part)) cuts[p].Add(hz);
                    cutLines.Add(line);
                    break;
                case "filter" when wired && Dip().IsMatch(value):
                    foreach (var p in Split(part)) dips[p] = line;
                    break;
                default: notUnderstood.Add(line); break;
            }
        }

        if (includesCabSound)
        {
            // CabSound's own config.txt, changed by something else: its filters live in CabSound.txt,
            // so whatever is found here is extra and the saved settings stay
            notUnderstood.AddRange(cutLines);
            notUnderstood.AddRange(dips.Values.Distinct());
            if (swapLine is not null) notUnderstood.Add(swapLine);
            return new FoundConfig(new SoundSettings(), ["Your saved CabSound sound settings (config.txt already used CabSound.txt)"],
                dropped, notUnderstood, [], KeepSavedSound: true);
        }

        var carried = new List<string>();
        var notes = new List<string>();
        var mode = LowCutMode.Original;
        bool Cuts(int[] backbox, int[] exciters) => cuts[Part.Backbox].SequenceEqual(backbox)
            && cuts[Part.Front].SequenceEqual(exciters) && cuts[Part.Rear].SequenceEqual(exciters);

        if (Cuts([90, 90], [90, 90])) carried.Add("Low cut: Original (two 90 Hz cuts on backbox and exciters)");
        else if (Cuts([40], [60]))
        {
            mode = LowCutMode.Gentle;
            carried.Add("Low cut: Gentle (backbox 40 Hz, exciters 60 Hz)");
        }
        else if (cutLines.Count == 0)
            notes.Add("No low cuts found. CabSound starts on Original, which adds two 90 Hz cuts on backbox and exciters.");
        else
        {
            notUnderstood.AddRange(cutLines);
            notes.Add("Low cuts found don't match Original or Gentle; CabSound starts on Original (two 90 Hz cuts on backbox and exciters).");
        }

        var dip = dips.Count == 3;
        if (dip) carried.Add("250 Hz dip (-2 dB) on backbox and exciters");
        else notUnderstood.AddRange(dips.Values.Distinct());
        if (swapped) carried.Add("Side/Rear jack swap");
        if (swapAboveLevels)
            notes.Add("The jack swap was above the level file, so the front and rear exciter levels acted on each other's speakers. " +
                      "CabSound applies levels before the swap: on tables where front and rear differ, the balance will be the other way round than in the last session.");

        return new FoundConfig(new SoundSettings(mode, dip, swapped), carried, dropped, notUnderstood, notes);
    }

    private static Part PartOf(string channels)
    {
        var set = channels.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (set.SetEquals(["ALL"])) return Part.All;
        if (set.SetEquals(["L", "R"])) return Part.Backbox;
        if (set.SetEquals(["RL", "RR"])) return Part.Front;
        if (set.SetEquals(["SL", "SR"])) return Part.Rear;
        if (set.SetEquals(["SL", "SR", "RL", "RR"])) return Part.Exciters;
        if (set.SetEquals(["SUB"])) return Part.Sub;
        return Part.Other;
    }

    private static Part[] Split(Part part) => part == Part.Exciters ? [Part.Front, Part.Rear] : [part];
}
