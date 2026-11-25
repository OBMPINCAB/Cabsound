using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class EqApoFileTests
{
    private static readonly GroupLevels NoCalibration = new(0, 0, 0, 0);
    private const int BoostLimitDb = 10;

    [Fact]
    public void Build_WritesTheThreeWiredGroups_LoweredByFixedHeadroom()
    {
        // no calibration, limit 10 -> headroom 10
        var text = EqApoFile.Build(new TableLevel("VP.X", 100, 66, -3, -4, -5), NoCalibration, Sound, subConnected: false, BoostLimitDb);

        Assert.Equal("-13 dB", Preamp(text, "L R"));
        Assert.Equal("-14 dB", Preamp(text, "SL SR")); // rear
        Assert.Equal("-15 dB", Preamp(text, "RL RR")); // front
    }

    [Fact]
    public void Build_NoSubLine_WhenJackNotConnected()
    {
        var text = EqApoFile.Build(new TableLevel("VP.X", 100, 66, 0, 0, 0, SubDb: -2), NoCalibration, Sound, subConnected: false, BoostLimitDb);

        Assert.DoesNotContain("SUB", text);
    }

    [Fact]
    public void Build_WritesSub_WhenJackConnected()
    {
        var text = EqApoFile.Build(new TableLevel("VP.X", 100, 66, -3, -4, -5, SubDb: -2), NoCalibration, Sound, subConnected: true, BoostLimitDb);

        Assert.Equal("-12 dB", Preamp(text, "SUB"));
    }

    [Fact]
    public void Build_AddsCalibrationToTableOffsets()
    {
        // highest possible group = rear -2 + 10 = 8 -> headroom 8
        var calibration = new GroupLevels(Backglass: -10, Rear: -2, Front: -4, Sub: 0);

        var text = EqApoFile.Build(new TableLevel("VP.X", 100, 66, 3, 1, 0), calibration, Sound, subConnected: false, BoostLimitDb);

        Assert.Equal("-15 dB", Preamp(text, "L R"));
        Assert.Equal("-9 dB", Preamp(text, "SL SR"));  // rear
        Assert.Equal("-12 dB", Preamp(text, "RL RR")); // front
    }

    [Fact]
    public void Build_PinVolCabinet_HeadroomCoversTheLargestPossibleBoost()
    {
        // PinVol's typical cabinet levels: exciters +10, offset may go to +10 -> headroom 20
        var calibration = new GroupLevels(-10, 10, 10, 0);

        var text = EqApoFile.Build(new TableLevel("VP.X", 100, 66, 0, 0, 0), calibration, Sound, subConnected: false, BoostLimitDb);

        Assert.Equal("-30 dB", Preamp(text, "L R"));
        Assert.Equal("-10 dB", Preamp(text, "RL RR"));
        Assert.Equal("-10 dB", Preamp(text, "SL SR"));
    }

    [Fact]
    public void Build_BoostLimit3_OnlyCosts3dB()
    {
        var calibration = new GroupLevels(-10, 10, 10, 0);

        var text = EqApoFile.Build(new TableLevel("VP.X", 100, 66, 0, 0, 0), calibration, Sound, subConnected: false, boostLimitDb: 3);

        Assert.Equal("-23 dB", Preamp(text, "L R"));
        Assert.Equal("-3 dB", Preamp(text, "SL SR"));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(10)]
    public void AppliedLevels_ChangingOneGroup_DoesNotMoveTheOthers(int frontOffset)
    {
        // Owner's bug: raising the exciters made everything quieter (auto headroom lowered the backbox)
        var calibration = new GroupLevels(-10, 10, 10, 0);

        var levels = EqApoFile.AppliedLevels(new TableLevel("VP.X", 100, 66, 0, 0, frontOffset), calibration, subConnected: false, BoostLimitDb);

        Assert.Equal(-30, levels.Backglass);
        Assert.Equal(-10, levels.Rear);
        Assert.Equal(-10 + frontOffset, levels.Front);
    }

    [Fact]
    public void AppliedLevels_NeverAboveZero_EvenForAnImportedOffsetBeyondTheLimit()
    {
        var levels = EqApoFile.AppliedLevels(new TableLevel("VP.X", 100, 66, 0, 0, 14), NoCalibration, subConnected: false, BoostLimitDb);

        Assert.Equal(0, levels.Front);
        Assert.Equal(-14, levels.Backglass);
    }

    [Fact]
    public void AppliedLevels_IgnoresSubCalibration_WhenJackNotConnected()
    {
        // A sub that isn't written must not make everything else quieter
        var levels = EqApoFile.AppliedLevels(new TableLevel("VP.X", 100, 66, -2, 0, 0), new GroupLevels(0, 0, 0, 8), subConnected: false, BoostLimitDb);

        Assert.Equal(-12, levels.Backglass);
        Assert.Equal(-10, levels.Rear);
    }

    [Fact]
    public void Build_HasDoNotEditHeaderAndPostMixStage()
    {
        var lines = EqApoFile.Build(new TableLevel("VP.X", 100, 66, 0, 0, 0), NoCalibration, Sound, subConnected: false, BoostLimitDb).Split("\r\n");

        Assert.Contains("do not edit", lines[0]);
        Assert.Equal("Stage: post-mix", lines[1]);
    }

    [Fact]
    public void Headroom_IsMaxCalibrationPlusBoost()
    {
        var calibration = new GroupLevels(-10, 10, 10, 0);

        Assert.Equal(13, EqApoFile.Headroom(new TableLevel("VP.X", 100, 66, 0, 0, 0), calibration, false, 3));
        Assert.Equal(16, EqApoFile.Headroom(new TableLevel("VP.X", 100, 66, 0, 0, 6), calibration, false, 3));
    }

    // --- CabSound.txt layout: filters, then levels, then the jack swap ---

    private static readonly SoundSettings Sound = new();
    private static readonly TableLevel T = new("VP.X", 100, 66, 0, 0, 0);
    private static readonly GroupLevels Cal = new(-10, 10, 10, 0); // boost limit 0 -> written -20 / 0 / 0
    private const string Hp90 = "Filter: ON HP Fc 90 Hz";
    private const string Dip = "Filter: ON PK Fc 250 Hz Gain -3.5 dB Q 1.0";
    private const string Clack = "Filter: ON PK Fc 2800 Hz Gain 2.5 dB Q 1.0";

    /// <summary>The lines of one "Channel:" block (up to the next Channel line).</summary>
    private static List<string> Block(string text, string channels) =>
        text.Split("\r\n").SkipWhile(l => l != $"Channel: {channels}").Skip(1).TakeWhile(l => !l.StartsWith("Channel:")).ToList();

    private static string Preamp(string text, string channels) =>
        Block(text, channels).Single(l => l.StartsWith("Preamp: "))["Preamp: ".Length..];

    [Fact]
    public void Build_Original_SwapOn_IsExactlyTheSpecExample()
    {
        var bbFilters = $"{Hp90}\r\n{Hp90}\r\n";
        var exFilters = $"{Hp90}\r\n{Hp90}\r\n{Dip}\r\n{Clack}\r\n";

        Assert.Equal(
            "# Updated by CabSound Automatically -- do not edit\r\nStage: post-mix\r\n" +
            "Channel: L R\r\n" + bbFilters + "Preamp: -20 dB\r\n" +
            "Channel: RL RR\r\n" + exFilters + "Preamp: 0 dB\r\n" +
            "Channel: SL SR\r\n" + exFilters + "Preamp: 0 dB\r\n" +
            "Channel: all\r\nCopy: SL=RL SR=RR RL=SL RR=SR\r\n",
            EqApoFile.Build(T, Cal, new SoundSettings(JacksSwapped: true), false, 0));
    }

    [Fact]
    public void Build_OneCutPerGroup_AsTheLevelsSay()
    {
        var text = EqApoFile.Build(T with { BackboxLowCut = 40, ExciterLowCut = 60 }, Cal, new SoundSettings(), false, 0);

        Assert.Equal(["Filter: ON HP Fc 40 Hz", "Preamp: -20 dB"], Block(text, "L R"));
        Assert.Equal(["Filter: ON HP Fc 60 Hz", Dip, Clack, "Preamp: 0 dB"], Block(text, "RL RR"));
        Assert.Equal(["Filter: ON HP Fc 60 Hz", Dip, Clack, "Preamp: 0 dB", ""], Block(text, "SL SR"));
    }

    [Fact]
    public void Build_SeparateFrontAndRearExciterLowCuts_WritesEachToItsChannelPair()
    {
        var text = EqApoFile.Build(T with { BackboxLowCut = 40, ExciterLowCut = 40, RearExciterLowCut = 90 }, Cal, new SoundSettings(), false, 0);

        Assert.Equal(["Filter: ON HP Fc 40 Hz", "Preamp: -20 dB"], Block(text, "L R"));
        Assert.Equal(["Filter: ON HP Fc 40 Hz", Dip, Clack, "Preamp: 0 dB"], Block(text, "RL RR"));
        Assert.Equal(["Filter: ON HP Fc 90 Hz", Dip, Clack, "Preamp: 0 dB", ""], Block(text, "SL SR"));
    }

    [Fact]
    public void Build_NightMode_ForcesAtLeast120HzLowCut_AndLowersExcitersBy3dB()
    {
        var text = EqApoFile.Build(T with { BackboxLowCut = 40, ExciterLowCut = 0, RearExciterLowCut = 150 }, Cal, new SoundSettings(), false, 0, nightMode: true);

        Assert.Equal(["Filter: ON HP Fc 120 Hz", "Preamp: -20 dB"], Block(text, "L R"));
        Assert.Equal(["Filter: ON HP Fc 120 Hz", Dip, Clack, "Preamp: -3 dB"], Block(text, "RL RR"));
        Assert.Equal(["Filter: ON HP Fc 150 Hz", Dip, Clack, "Preamp: -3 dB", ""], Block(text, "SL SR"));
    }

    [Fact]
    public void Build_IgnoresTheOldCabinetLowCutSettings()
    {
        var text = EqApoFile.Build(new TableLevel("VP.X", 100, 66, 0, 0, 0), new GroupLevels(0, 0, 0, 0),
            new SoundSettings(LowCutMode.Gentle, BackboxCut: 40, ExciterCut: 60), false, 6);

        Assert.Contains("Channel: L R\r\nFilter: ON HP Fc 90 Hz\r\nFilter: ON HP Fc 90 Hz\r\n", text);
    }

    [Fact]
    public void Build_TableLowCut_OverridesTheCabinetSetting()
    {
        var text = EqApoFile.Build(T with { BackboxLowCut = 120, ExciterLowCut = 0 }, Cal, Sound, false, 0);

        Assert.Equal(["Filter: ON HP Fc 120 Hz", "Preamp: -20 dB"], Block(text, "L R"));
        Assert.Equal([Dip, Clack, "Preamp: 0 dB"], Block(text, "RL RR"));
        Assert.DoesNotContain(Block(text, "SL SR"), l => l.Contains(" HP "));
    }

    [Fact]
    public void Build_DipOff_NoPkLines()
    {
        Assert.DoesNotContain(" PK ", EqApoFile.Build(T, Cal, new SoundSettings(Dip250: false), false, 0));
    }

    [Fact]
    public void Build_BassPunch_Writes72HzBoostAnd185HzMudCutOnBackboxOnly()
    {
        var text = EqApoFile.Build(T with { BackboxLowCut = 40, ExciterLowCut = 40 }, Cal,
            new SoundSettings(BassPunch: true), false, 0);

        Assert.Equal([
            "Filter: ON HP Fc 40 Hz",
            EqApoFile.BassPunchLine,
            EqApoFile.BassMudLine,
            "Preamp: -20 dB",
        ], Block(text, "L R"));
        Assert.DoesNotContain(Block(text, "RL RR"), l => l == EqApoFile.BassPunchLine || l == EqApoFile.BassMudLine);
        Assert.DoesNotContain(Block(text, "SL SR"), l => l == EqApoFile.BassPunchLine || l == EqApoFile.BassMudLine);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public void Build_ShakerBoost_Writes42HzPeakOnBothExciterPairsOnly(int boostDb)
    {
        var text = EqApoFile.Build(T with { BackboxLowCut = 40, ExciterLowCut = 40, RearExciterLowCut = 50 }, Cal,
            new SoundSettings(ShakerBoostDb: boostDb), false, 0);

        var expected = $"Filter: ON PK Fc 42 Hz Gain {boostDb} dB Q 1.6";
        Assert.DoesNotContain(Block(text, "L R"), l => l.Contains("42 Hz"));
        Assert.Equal(["Filter: ON HP Fc 40 Hz", expected, Dip, Clack, "Preamp: 0 dB"], Block(text, "RL RR"));
        Assert.Equal(["Filter: ON HP Fc 50 Hz", expected, Dip, Clack, "Preamp: 0 dB", ""], Block(text, "SL SR"));
    }

    [Fact]
    public void Build_NightMode_SuppressesBassPunchAndShakerBoost()
    {
        var text = EqApoFile.Build(T with { BackboxLowCut = 40, ExciterLowCut = 40 }, Cal,
            new SoundSettings(BassPunch: true, ShakerBoostDb: 6), false, 0, nightMode: true);

        Assert.DoesNotContain("72 Hz", text);
        Assert.DoesNotContain("42 Hz", text);
    }

    [Fact]
    public void Build_SwapOff_NoCopyLine_AndNoTrailingChannelAll()
    {
        var text = EqApoFile.Build(T, Cal, Sound, false, 0);

        Assert.DoesNotContain("Copy:", text);
        Assert.DoesNotContain("Channel: all", text);
        Assert.EndsWith("Preamp: 0 dB\r\n", text);
    }

    [Fact]
    public void Build_Sub_AfterRear_BeforeSwap_LevelOnly()
    {
        var lines = EqApoFile.Build(T, Cal, new SoundSettings(JacksSwapped: true), true, 0).Split("\r\n").ToList();

        Assert.True(lines.IndexOf("Channel: SL SR") < lines.IndexOf("Channel: SUB"));
        Assert.True(lines.IndexOf("Channel: SUB") < lines.IndexOf("Channel: all"));
        Assert.StartsWith("Preamp: ", lines[lines.IndexOf("Channel: SUB") + 1]);
    }

    [Fact]
    public void Build_FrontLevelIsOnRlRr_RearOnSlSr()
    {
        // VPX (SSF standard) sends the player end of the table to RL RR and the backbox end to SL SR.
        // Measured on the cabinet 2026-11-11: flippers arrive on RL RR.
        var text = EqApoFile.Build(T with { FrontDb = -5, RearDb = -4 }, Cal, Sound, false, 0);

        Assert.Equal("-5 dB", Preamp(text, "RL RR"));
        Assert.Equal("-4 dB", Preamp(text, "SL SR"));
    }

    [Fact]
    public void FileName_IsCabSoundTxt() => Assert.Equal("CabSound.txt", EqApoFile.FileName);

    // --- Calibration mode: levels exactly as given ---

    [Fact]
    public void BuildCalibration_WritesLevelsAsGiven()
    {
        var text = EqApoFile.BuildCalibration(new GroupLevels(Backglass: -6, Rear: -11, Front: -9, Sub: 0), null, Sound);

        var bbFilters = $"{Hp90}\r\n{Hp90}\r\n";
        var exFilters = $"{Hp90}\r\n{Hp90}\r\n{Dip}\r\n{Clack}\r\n";
        Assert.Equal("# Updated by CabSound Automatically -- do not edit\r\nStage: post-mix\r\n"
            + "Channel: L R\r\n" + bbFilters + "Preamp: -6 dB\r\n"
            + "Channel: RL RR\r\n" + exFilters + "Preamp: -9 dB\r\n"
            + "Channel: SL SR\r\n" + exFilters + "Preamp: -11 dB\r\n", text);
    }

    [Theory]
    [InlineData(60, "Filter: ON HP Fc 60 Hz\r\n")]
    [InlineData(0, "")]
    public void BuildCalibration_ExciterCut_OnlyOnTheExciters(int cut, string exciterFilter)
    {
        var text = EqApoFile.BuildCalibration(new GroupLevels(-6, -6, -6, 0), cut, Sound);

        Assert.Equal("# Updated by CabSound Automatically -- do not edit\r\nStage: post-mix\r\n"
            + $"Channel: L R\r\n{Hp90}\r\n{Hp90}\r\nPreamp: -6 dB\r\n"
            + $"Channel: RL RR\r\n{exciterFilter}{Dip}\r\n{Clack}\r\nPreamp: -6 dB\r\n"
            + $"Channel: SL SR\r\n{exciterFilter}{Dip}\r\n{Clack}\r\nPreamp: -6 dB\r\n", text);
    }

    [Fact]
    public void BuildCalibration_NeverAboveZero()
    {
        var text = EqApoFile.BuildCalibration(new GroupLevels(Backglass: 4, Rear: 0, Front: 2, Sub: 0), null, Sound);

        Assert.Equal("0 dB", Preamp(text, "L R"));
        Assert.Equal("0 dB", Preamp(text, "RL RR"));
        Assert.Equal("0 dB", Preamp(text, "SL SR"));
    }

    [Fact]
    public void BuildCalibration_KeepsDipAndJackSwap_NeverASubBlock()
    {
        var text = EqApoFile.BuildCalibration(new GroupLevels(-6, -6, -6, -6), null, Sound with { Dip250 = false, JacksSwapped = true });

        Assert.DoesNotContain(Dip, text);
        Assert.DoesNotContain("SUB", text);
        Assert.EndsWith("Preamp: -6 dB\r\nChannel: all\r\n" + EqApoFile.JackSwapLine + "\r\n", text);
    }

    [Fact]
    public void BuildCalibration_AllowsCustomBackboxCut_ForSubTesting()
    {
        var text = EqApoFile.BuildCalibration(new GroupLevels(-6, -20, -20, 0), exciterCut: 90, Sound, backboxCut: 40);

        Assert.Equal(["Filter: ON HP Fc 40 Hz", "Preamp: -6 dB"], Block(text, "L R"));
    }
}
