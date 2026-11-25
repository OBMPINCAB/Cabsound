using System.Text.Json.Serialization;

namespace CabSound.Core;

/// <summary>
/// Legacy cabinet-wide low cut mode (from PinVol config.txt take-over). Original = two 90 Hz cuts on
/// backbox and exciters. Gentle = one cut, backbox 40 Hz / exciters 60 Hz.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LowCutMode>))]
public enum LowCutMode { Original, Gentle }

/// <summary>The groups a low cut can be set for: Backbox (sub), Front exciters (shaker), Rear exciters.</summary>
public enum LowCutGroup { Backbox, Exciters, RearExciters }

/// <summary>
/// Cabinet-wide sound settings written into CabSound.txt. Dip250 = the exciter clarity EQ (-3.5 dB mud cut
/// at 250 Hz + +2.5 dB clack boost at 2.8 kHz on exciters; backbox clean). BassPunch = punchy bass EQ on
/// L R (+2.5 dB punch at 72 Hz + -3.5 dB cabinet/backbox mud cut at 185 Hz). ShakerBoostDb = narrow 42 Hz
/// resonant boost (0 / 3 / 6 dB) on both exciter pairs so the CSD amp's bass shaker hits harder without
/// raising exciter volume. JacksSwapped = the Windows driver swapped the Side and Rear jacks, so CabSound
/// swaps them back. RattleWarned = the one-time warning about low exciter cuts has been shown.
/// BackboxCut / ExciterCut / RearExciterCut = cabinet-wide low cuts (Hz, 0 = off, null = follow LowCut).
/// </summary>
public sealed record SoundSettings(
    LowCutMode LowCut = LowCutMode.Original,
    bool Dip250 = true,
    bool JacksSwapped = false,
    bool RattleWarned = false,
    int? BackboxCut = null,
    int? ExciterCut = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RearExciterCut = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool BassPunch = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ShakerBoostDb = 0)
{
    /// <summary>Sets one group's cabinet-wide low cut while preserving the other groups' effective cuts.</summary>
    public SoundSettings WithCabinetCut(LowCutGroup group, int? cut)
    {
        var bb = group == LowCutGroup.Backbox ? cut : LowCutChoices.Cabinet(this, LowCutGroup.Backbox);
        var ex = group == LowCutGroup.Exciters ? cut : LowCutChoices.Cabinet(this, LowCutGroup.Exciters);
        var rear = group == LowCutGroup.RearExciters ? cut : RearExciterCut; // null = keeps following the front cut
        return this with { LowCut = LowCutMode.Original, BackboxCut = bb, ExciterCut = ex, RearExciterCut = rear };
    }
}

/// <summary>Low-cut choices for the cabinet and per-table overrides, least bass first: null = cabinet's cut, 0 = off, else Hz.</summary>
public static class LowCutChoices
{
    public static readonly IReadOnlyList<int?> All = [null, 150, 120, 90, 60, 50, 40, 0];

    /// <summary>The next (+1 = more bass / lower cut) or previous (-1 = less bass / toward Cabinet's cut) choice; stops at the ends.</summary>
    public static int? Step(int? current, int direction)
    {
        static int Rank(int? c) => c is null ? -1000 : -c.Value;
        var rank = Rank(current);
        return direction > 0
            ? All.FirstOrDefault(c => Rank(c) > rank, All[^1])
            : All.LastOrDefault(c => Rank(c) < rank, All[0]);
    }

    public static string Label(int? value) => value switch
    {
        null => "Cabinet's cut",
        0 => "Off",
        _ => $"{value} Hz",
    };

    /// <summary>Cabinet-wide choices, least bass first; null = the two original 90 Hz cuts.</summary>
    public static readonly IReadOnlyList<int?> CabinetChoices = [null, 150, 120, 90, 60, 50, 40, 0];

    public static string CabinetLabel(int? value) => value switch
    {
        null => "Two 90 Hz cuts (original)",
        0 => "Off",
        _ => $"{value} Hz",
    };

    /// <summary>The cabinet-wide low cut for a group, as one of CabinetChoices; Gentle counts as 40 / 60 Hz.</summary>
    public static int? Cabinet(SoundSettings sound, LowCutGroup group) =>
        (group switch
        {
            LowCutGroup.Backbox => sound.BackboxCut,
            LowCutGroup.Exciters => sound.ExciterCut,
            _ => sound.RearExciterCut ?? sound.ExciterCut,
        })
        ?? (sound.LowCut == LowCutMode.Original ? null : group == LowCutGroup.Backbox ? 40 : 60);

    /// <summary>What low cut a table actually gets: its own override, else the cabinet's cut.</summary>
    public static int? Effective(SoundSettings sound, LowCutGroup group, int? tableCut) =>
        tableCut ?? Cabinet(sound, group);

    /// <summary>A table's low cut in words: its own cut, or "Cabinet's cut" with what that is right now.</summary>
    public static string TableLabel(SoundSettings sound, LowCutGroup group, int? tableCut) =>
        tableCut is not null ? Label(tableCut) : Cabinet(sound, group) switch
        {
            null => "Cabinet's cut (2× 90 Hz)",
            var cut => $"Cabinet's cut ({Label(cut)})",
        };

    /// <summary>May the exciters rattle with this table's effective cut? True for Off or under 60 Hz.</summary>
    public static bool IsRisky(SoundSettings sound, LowCutGroup group, int? tableCut) =>
        IsRisky(group, Effective(sound, group, tableCut));

    /// <summary>May the exciters rattle with this cut (null = two 90 Hz cuts)? True for Off (0) or under 60 Hz.</summary>
    public static bool IsRisky(LowCutGroup group, int? value) =>
        group is LowCutGroup.Exciters or LowCutGroup.RearExciters && value is < 60;
}
