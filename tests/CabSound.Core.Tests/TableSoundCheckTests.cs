using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class TableSoundCheckTests
{
    private static SoundInfo Table(string name, int channels = 1, int bits = 16) => new(name, SoundOutput.Table, channels, bits, true);
    private static SoundInfo Bg(string name) => new(name, SoundOutput.Backglass, 2, 16, true);

    private static List<SoundFinding> Run(string script, params SoundInfo[] sounds) =>
        TableSoundCheck.Run(script.Replace("\n", "\r\n"), sounds).ToList();

    private const string Placed = "Sub PlaySoundAt(s, o)\n  PlaySound s, 1, 1, AudioPan(o), 0, 0, 0, 1, AudioFade(o)\nEnd Sub\n";

    private static SoundInfo WithAudio(string name, double peakDb = -6, double? corr = 0.9, bool sumClips = false, SoundOutput output = SoundOutput.Table) =>
        new(name, output, 2, 16, true, new AudioFacts(peakDb, corr, sumClips));

    [Fact]
    public void Stereo_NoLongerAFinding()
    {
        Assert.Empty(Run(Placed + "PlaySoundAt \"sling\", s\n", Table("sling", 2), Table("bats", 2, 24), Bg("music"), WithAudio("rub")));
    }

    [Fact]
    public void OppositePhase_FlaggedAtMinus08_NotAtMinus05()
    {
        var f = Run(Placed, WithAudio("flat", corr: -0.8), WithAudio("wide", corr: -0.5), WithAudio("bgflat", corr: -0.9, output: SoundOutput.Backglass));

        Assert.Equal([(FindingKind.OppositePhase, "flat")], f.Select(x => (x.Kind, x.Sound)));
    }

    [Fact]
    public void SilentAudio_Flagged_BelowMinus60_NotAtMinus50()
    {
        var f = Run(Placed, WithAudio("zero", peakDb: double.NegativeInfinity, corr: null), WithAudio("faint", peakDb: -61),
            WithAudio("quiet", peakDb: -50), WithAudio("bgzero", peakDb: double.NegativeInfinity, corr: null, output: SoundOutput.Backglass));

        Assert.Equal(["zero", "faint", "bgzero"], f.Where(x => x.Kind == FindingKind.SilentAudio).Select(x => x.Sound));
    }

    [Fact]
    public void RollMissing_InsteadOfMissing()
    {
        var f = Run(Placed + "PlaySoundAt \"BallRoll_\" & b, ball\n", Table("other"));

        var m = Assert.Single(f);
        Assert.Equal((FindingKind.RollMissing, "BallRoll_*", 4), (m.Kind, m.Sound, m.Line));
    }

    [Fact]
    public void RollUnplaced_InsteadOfUnplaced()
    {
        var f = Run(Placed + "PlaySound \"fx_ballrolling\", -1, v\nPlaySoundAt \"fx_ballrolling\", ball\n", Table("fx_ballrolling"));

        var m = Assert.Single(f);
        Assert.Equal((FindingKind.RollUnplaced, "fx_ballrolling", 4), (m.Kind, m.Sound, m.Line));
    }

    [Theory]
    [InlineData("rollover", FindingKind.MissingSound)]       // a switch, not ball rolling
    [InlineData("fx_TrollDown", FindingKind.MissingSound)]
    [InlineData("fx_scroll", FindingKind.MissingSound)]
    [InlineData("plasticroll", FindingKind.RollMissing)]
    [InlineData("fx_ballrolling0", FindingKind.RollMissing)]
    [InlineData("WireRoll", FindingKind.RollMissing)]
    public void RollNames_NotRolloverTrollOrScroll(string name, FindingKind expected)
    {
        Assert.Equal(expected, Assert.Single(Run(Placed + $"PlaySoundAt \"{name}\", ball\n")).Kind);
    }

    [Fact]
    public void RollClips_OnlyForRollSounds()
    {
        var f = Run(Placed, WithAudio("RampRoll", sumClips: true), WithAudio("sling", sumClips: true));

        Assert.Equal([(FindingKind.RollClips, "RampRoll")], f.Select(x => (x.Kind, x.Sound)));
    }

    [Fact]
    public void MissingSound_ReportedWithLine_IgnoringCaseCommentsAndStopSound()
    {
        var script = Placed +
            "PlaySoundAt \"Fx_Kicker\", k\n" +             // present, other case
            "PlaySoundAt \"fx_plunger\", Plunger\n" +       // line 5: missing
            "' PlaySoundAt \"commented\", x\n" +
            "x = 1 ' PlaySound \"trailing\"\n" +
            "StopSound \"gone\"\n" +
            "MsgBox \"fx_not_a_sound\"\n";

        var f = Run(script, Table("fx_kicker"));

        var m = Assert.Single(f);
        Assert.Equal((FindingKind.MissingSound, "fx_plunger", 5), (m.Kind, m.Sound, m.Line));
        Assert.Equal("PlaySoundAt \"fx_plunger\", Plunger", m.Code);
    }

    [Fact]
    public void MissingSound_FindsNamesInsideTimerStringsAndSecondArguments()
    {
        var script = Placed +
            "vpmTimer.AddTimer 500, \"PlaySoundVol \"\"say-greatScore\"\", VolDef '\"\n" +
            ".InitExitSnd SoundFX(\"fx_kicker\", DOFContactors), SoundFX(\"fx_solenoid\", DOFContactors)\n";

        Assert.Equal(["say-greatScore", "fx_solenoid"], Run(script, Table("fx_kicker")).Select(x => x.Sound));
    }

    [Fact]
    public void MissingSound_NameBuiltWithAmpersand_OkWhenAnySoundStartsWithIt()
    {
        var script = Placed + "PlaySoundAt \"fx_bumper\" & INT(RND*4)+1, b\nPlaySoundVol \"sfx-zap\" & n, 1\n";

        var m = Assert.Single(Run(script, Table("fx_bumper1")));
        Assert.Equal((FindingKind.MissingSound, "sfx-zap*"), (m.Kind, m.Sound));
    }

    [Fact]
    public void HitVolumeAdded_Found()
    {
        var script = Placed + "  PlaySound \"rubber\", 0, Vol(ActiveBall)+VolTable, AudioPan(ActiveBall), 0, 0, 1, 0, AudioFade(ActiveBall)\n" +
                     "  PlaySound \"rubber\", 0, Vol(ActiveBall) * VolTable, AudioPan(ActiveBall), 0, 0, 1, 0, AudioFade(ActiveBall)\n";

        var m = Assert.Single(Run(script, Table("rubber")));
        Assert.Equal((FindingKind.HitVolumeAdded, 4), (m.Kind, m.Line));
    }

    [Fact]
    public void UnplacedTableSound_Found_BackglassAndPlacedOnesAreFine()
    {
        var script = Placed +
            "PlaySoundVol \"popper_ball\", VolTable\n" +     // line 4: table sound, no position
            "PlaySound \"fx_saucer\"\n" +                    // line 5
            "PlaySoundVol \"sfx-bonus\", VolSfx\n" +         // backglass: fine
            "PlaySoundAt \"popper_ball\", Kicker1\n" +       // placed
            "PlaySound \"drain\", 0, 1, AudioPan(Drain), 0.25, 0, 0, 1, AudioFade(Drain)\n";

        var f = Run(script, Table("popper_ball"), Table("fx_saucer"), Bg("sfx-bonus"), Table("drain"));

        Assert.Equal([("popper_ball", 4), ("fx_saucer", 5)], f.Where(x => x.Kind == FindingKind.UnplacedSound).Select(x => (x.Sound, x.Line)));
    }

    [Fact]
    public void SoundNameInAConst_IsChecked()
    {
        var script = Placed + "Const SFlipperOn = \"FlipperUp\"\nConst SMissing = \"nope\"\n" +
                     "PlaySound SoundFX (SFlipperOn,DOFContactors):LeftFlipper.RotateToEnd\n" +   // line 6: unplaced
                     "PlaySoundAt SFlipperOn, LeftFlipper\n" +                                    // placed
                     "PlaySound SMissing\n";                                                      // line 8: missing

        var f = Run(script, Table("FlipperUp"));

        Assert.Equal([(FindingKind.UnplacedSound, "FlipperUp", 6), (FindingKind.MissingSound, "nope", 8)], f.Select(x => (x.Kind, x.Sound, x.Line)));
    }

    [Fact]
    public void ConstThatStartsABuiltName_IsNotChecked()
    {
        var script = Placed + "Const Cartridge_Cabinet_Sounds = \"SY_TNA_REV02\"\n" +
                     "PlaySoundAt (Cartridge_Cabinet_Sounds & \"_Start_Button\"), StartButtonPosition\n";

        Assert.Empty(Run(script, Table("SY_TNA_REV02_Start_Button")));
    }

    [Fact]
    public void SubDefinedTwice_OnlyTheLastCounts()
    {
        // VBScript runs the last definition; the first one is dead code.
        var script = Placed +
            "Sub RandomSoundRubber()\n  PlaySound \"fx_rubber2\", 0, 1\nEnd Sub\n" +
            "Sub RandomSoundRubber()\n  PlaySound \"rubber_hit_1\", 0, Vol(ActiveBall)+1, 0\nEnd Sub\n";

        var f = Run(script, Table("rubber_hit_1"));

        Assert.Equal([(FindingKind.HitVolumeAdded, 8), (FindingKind.UnplacedSound, 8)], f.Select(x => (x.Kind, x.Line)));
    }

    [Fact]
    public void WrapperCallingAPlacingWrapper_CountsAsPlaced()
    {
        var script = Placed + "Sub PlaySoundAtBall(s)\n  PlaySoundAt s, ActiveBall\nEnd Sub\n" +
                     "Sub PlaySoundAtBallVol(s, v)\n  PlaySoundAtBall s\nEnd Sub\n" +
                     "PlaySoundAtBall \"fx_target\"\nPlaySoundAtBallVol \"fx_target\", 2\n";

        Assert.Empty(Run(script, Table("fx_target")));
    }

    [Fact]
    public void ContinuedStatement_FadeOnNextLine_CountsAsPlaced()
    {
        var script = Placed + "PlaySound \"RampLoop\" & x, -1, _\n    1, AudioPan(b), 0, 0, 1, 0, AudioFade(b)\n";

        Assert.Empty(Run(script, Table("RampLoop1")));
    }

    [Fact]
    public void EmptyNames_AreNotSounds()
    {
        Assert.Empty(Run(Placed + ".InitExitSounds \"\", \"\"\n.initsnd \"\", \"\"\n"));
    }

    [Theory]
    [InlineData("Nudge_1")]
    [InlineData("fx_nudge")]
    [InlineData("fx_coin")]
    [InlineData("Coin_In_1")]
    [InlineData("Start_Button")]
    [InlineData("SY_TNA_REV02_Shaker_Level_02_RampDown_Only")]
    [InlineData("fx_tilt")]
    public void CabinetSounds_UnplacedOnPurpose_NotFlagged(string name)
    {
        Assert.Empty(Run(Placed + $"PlaySound \"{name}\", 0, 1, 0, 0.25\n", Table(name)));
    }

    [Fact]
    public void NoFrontRear_WhenNoPlaySoundUsesFade()
    {
        var script = "Sub PlaySoundAt(s, o)\n  PlaySound s, 1, 1, AudioPan(o)\nEnd Sub\nPlaySoundAt \"flip\", f\n";

        Assert.Contains(Run(script, Table("flip")), x => x.Kind == FindingKind.NoFrontRear);
        Assert.DoesNotContain(Run(Placed, Table("flip")), x => x.Kind == FindingKind.NoFrontRear);
    }

    [Fact]
    public void KnockerOnBackglass_Found()
    {
        var script = Placed + "vpmTimer.AddTimer 500, \"PlaySoundAtVol SoundFXDOF(\"\"sfx-knock\"\", 122, DOFPulse, DOFKnocker), B2, 1 '\"\n";

        var m = Assert.Single(Run(script, Bg("sfx-knock")), x => x.Kind == FindingKind.KnockerOnBackglass);
        Assert.Equal(("sfx-knock", 4), (m.Sound, m.Line));
    }

    [Theory]
    [InlineData("ballvol = Vol(BOT(b) ) * 10", true)]
    [InlineData("ballvol = Vol(BOT(b)) * 5", true)]
    [InlineData("ballvol = Vol(BOT(b) ) * 3", false)]
    public void LoudRoll_WhenVolumeMultipliedByFiveOrMore(string line, bool expected)
    {
        Assert.Equal(expected, Run(Placed + line + "\n").Any(x => x.Kind == FindingKind.LoudRoll));
    }

    [Fact]
    public void DofOnlyShaker_FlaggedWhenShakerSubHasDofWithoutPlaySound()
    {
        var ghostbusters = Placed +
            "Sub SolShaker(Enabled)\n" +
            "  If Enabled Then\n" +
            "    DOFALT 123, DOFOn\n" +
            "  Else\n" +
            "    DOFALT 123, DOFOff\n" +
            "  End If\n" +
            "End Sub\n";
        var afmWithSound = Placed +
            "Sub SolUfoShake(Enabled)\n" +
            "  If Enabled Then PlaySoundAt \"SaucerShake\", Ufo\n" +
            "End Sub\n";

        var f = Run(ghostbusters);
        var m = Assert.Single(f);
        Assert.Equal((FindingKind.DofOnlyShaker, "SolShaker", 4), (m.Kind, m.Sound, m.Line));

        Assert.Empty(Run(afmWithSound, Table("SaucerShake")));
    }

    [Fact]
    public void CleanScript_NoFindings()
    {
        Assert.Empty(Run(Placed + "PlaySoundAt \"flip\", LeftFlipper\n", Table("flip"), Bg("callout")));
    }
}
