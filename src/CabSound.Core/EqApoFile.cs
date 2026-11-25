using System.Text;

namespace CabSound.Core;

/// <summary>dB per SSF group. Used for the cabinet calibration and for the levels actually written.</summary>
public readonly record struct GroupLevels(int Backglass, int Rear, int Front, int Sub);

/// <summary>
/// Builds CabSound.txt, the one Equalizer APO file CabSound writes (config.txt only includes it).
/// Fixed order: per group the low cut and exciter clarity EQ, then the level; the Side/Rear jack swap last,
/// so every line above it names the channels as the tables use them: VPX (SSF standard) sends the
/// player end of the table to RL RR (front exciters) and the backbox end to SL SR (rear exciters).
/// Level per group = cabinet calibration + table offset - headroom (-3 dB on exciters in night mode).
/// SUB is only written when the C/SUB jack is connected. Clipping protection uses a FIXED headroom:
/// every group is lowered by the largest boost any table could reach (calibration + boost limit).
/// </summary>
public static class EqApoFile
{
    public const string FileName = "CabSound.txt";
    public const string JackSwapLine = "Copy: SL=RL SR=RR RL=SL RR=SR";
    public const string DipLine = "Filter: ON PK Fc 250 Hz Gain -3.5 dB Q 1.0";
    public const string ClackLine = "Filter: ON PK Fc 2800 Hz Gain 2.5 dB Q 1.0";
    public const string BassPunchLine = "Filter: ON PK Fc 72 Hz Gain 2.5 dB Q 1.4";
    public const string BassMudLine = "Filter: ON PK Fc 185 Hz Gain -3.5 dB Q 1.1";
    public static readonly IReadOnlyList<int> ShakerBoostChoices = [0, 3, 6];
    public static string ShakerBoostLine(int db) => $"Filter: ON PK Fc 42 Hz Gain {db} dB Q 1.6";
    public const int NightLowCutHz = 120;
    public const int NightExciterTrimDb = 3;

    public static GroupLevels AppliedLevels(TableLevel t, GroupLevels calibration, bool subConnected, int boostLimitDb, bool nightMode = false)
    {
        var headroom = Headroom(t, calibration, subConnected, boostLimitDb);
        var trim = nightMode ? NightExciterTrimDb : 0;
        return new GroupLevels(
            calibration.Backglass + t.BackglassDb - headroom,
            calibration.Rear + t.RearDb - headroom - trim,
            calibration.Front + t.FrontDb - headroom - trim,
            calibration.Sub + t.SubDb - headroom);
    }

    /// <summary>How far every group is lowered: the largest boost any table could reach (calibration + boost limit).</summary>
    public static int Headroom(TableLevel t, GroupLevels calibration, bool subConnected, int boostLimitDb)
    {
        var possible = subConnected
            ? new[] { calibration.Backglass, calibration.Rear, calibration.Front, calibration.Sub }
            : new[] { calibration.Backglass, calibration.Rear, calibration.Front };
        var totals = new[] { calibration.Backglass + t.BackglassDb, calibration.Rear + t.RearDb, calibration.Front + t.FrontDb };
        var written = subConnected ? [.. totals, calibration.Sub + t.SubDb] : totals;
        // written.Max() only matters for an imported offset beyond the limit
        return Math.Max(0, Math.Max(possible.Max() + boostLimitDb, written.Max()));
    }

    /// <summary>
    /// t = what the table really gets: levels added up, low cuts filled in.
    /// From sound only the exciter clarity EQ (Dip250) and the jack swap are written.
    /// In night mode, all groups get at least a 120 Hz low cut and exciters are lowered by 3 dB.
    /// </summary>
    public static string Build(TableLevel t, GroupLevels calibration, SoundSettings sound, bool subConnected, int boostLimitDb, bool nightMode = false)
    {
        var a = AppliedLevels(t, calibration, subConnected, boostLimitDb, nightMode);
        var rearCut = t.RearExciterLowCut ?? t.ExciterLowCut;
        if (nightMode)
        {
            return Write(a, NightCut(t.BackboxLowCut), NightCut(t.ExciterLowCut), NightCut(rearCut),
                sound with { BassPunch = false, ShakerBoostDb = 0 }, subConnected);
        }
        return Write(a, t.BackboxLowCut, t.ExciterLowCut, rearCut, sound, subConnected);
    }

    private static int NightCut(int? cut) => Math.Max(cut ?? 90, NightLowCutHz);

    /// <summary>
    /// The file while a calibration window is open: the levels exactly as given (never above 0 dB), no
    /// preset or table in it. Backbox with backboxCut (default: two original 90 Hz cuts), exciters with exciterCut.
    /// The exciter clarity EQ and the jack swap stay as set; never a SUB block.
    /// </summary>
    public static string BuildCalibration(GroupLevels written, int? exciterCut, SoundSettings sound, int? backboxCut = null)
    {
        var levels = new GroupLevels(Math.Min(0, written.Backglass), Math.Min(0, written.Rear), Math.Min(0, written.Front), 0);
        return Write(levels, backboxCut, exciterCut, exciterCut, sound, subConnected: false);
    }

    private static string Write(GroupLevels a, int? backboxCut, int? frontExciterCut, int? rearExciterCut, SoundSettings sound, bool subConnected)
    {
        var text = new StringBuilder("# Updated by CabSound Automatically -- do not edit\r\nStage: post-mix\r\n");

        void Group(string channels, int? cut, int levelDb, bool isExciter)
        {
            text.Append($"Channel: {channels}\r\n");
            foreach (var hz in LowCuts(cut)) text.Append($"Filter: ON HP Fc {hz} Hz\r\n");
            if (!isExciter && sound.BassPunch)
            {
                text.Append(BassPunchLine + "\r\n");
                text.Append(BassMudLine + "\r\n");
            }
            if (isExciter && sound.ShakerBoostDb > 0)
            {
                text.Append(ShakerBoostLine(sound.ShakerBoostDb) + "\r\n");
            }
            if (isExciter && sound.Dip250)
            {
                text.Append(DipLine + "\r\n");
                text.Append(ClackLine + "\r\n");
            }
            text.Append($"Preamp: {levelDb} dB\r\n");
        }

        Group("L R", backboxCut, a.Backglass, isExciter: false);
        Group("RL RR", frontExciterCut, a.Front, isExciter: true);
        Group("SL SR", rearExciterCut, a.Rear, isExciter: true);
        if (subConnected) text.Append($"Channel: SUB\r\nPreamp: {a.Sub} dB\r\n");
        if (sound.JacksSwapped) text.Append("Channel: all\r\n" + JackSwapLine + "\r\n");
        return text.ToString();
    }

    /// <summary>The low-cut frequencies written for a group, one filter line each: null = the two original 90 Hz cuts, 0 = none.</summary>
    public static int[] LowCuts(int? cut) => cut switch
    {
        0 => [],
        { } hz => [hz],
        null => [90, 90],
    };
}
