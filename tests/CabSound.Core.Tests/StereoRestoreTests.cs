using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class StereoRestoreTests
{
    private static VpxSound StereoTable(string name, short l = 1000, short r = 3000, int frames = 50) =>
        VpxSound.Parse(VpxSoundTests.Stream(name, name + ".wav", 2, 16,
            Enumerable.Range(0, frames).SelectMany(i => BitConverter.GetBytes((short)(l + i)).Concat(BitConverter.GetBytes((short)(r - i)))).ToArray()));

    private static List<(string Stream, VpxSound Sound)> One(string stream, VpxSound s) => [(stream, s)];

    [Fact]
    public void OldestBackup_PicksEarliestStamp()
    {
        string[] names = ["T.vpx.20261114-034113.bak", "T.vpx.20261113-140050.bak", "T.vbs.20261101-000000.bak", "T.vpx.20261113-150000-before-mono.bak"];

        Assert.Equal("T.vpx.20261113-140050.bak", StereoRestore.OldestBackup("T.vpx", names));
    }

    [Fact]
    public void OldestBackup_NoneOrOtherTable_Null()
    {
        Assert.Null(StereoRestore.OldestBackup("T.vpx", ["Other.vpx.20261113-140050.bak", "T.vbs.20261113-140050.bak"]));
        Assert.Null(StereoRestore.OldestBackup("T.vpx", []));
    }

    [Fact]
    public void Plan_MonoStep_Restore()
    {
        var before = StereoTable("sling");

        var item = Assert.Single(StereoRestore.Plan(One("Sound3", before.MakeMono().Mono), One("Sound3", before)));

        Assert.Equal(("Sound3", "sling", RestoreVerdict.Restore, ""), (item.Stream, item.Name, item.Verdict, item.Reason));
        Assert.NotNull(item.Before);
    }

    [Fact]
    public void Plan_MovedToBackglassAfterMono_RestoredOutputKept()
    {
        var before = StereoTable("sc_boom");
        var now = before.MakeMono().Mono.WithOutput(SoundOutput.Backglass);

        var item = Assert.Single(StereoRestore.Plan(One("Sound1", now), One("Sound1", before)));

        Assert.Equal((RestoreVerdict.Restore, SoundOutput.Backglass), (item.Verdict, item.Output));
    }

    [Fact]
    public void Plan_AlreadyStereoAgain_NotListed()
    {
        var before = StereoTable("gameloop");

        Assert.Empty(StereoRestore.Plan(One("Sound1", before.WithOutput(SoundOutput.Backglass)), One("Sound1", before)));
    }

    [Fact]
    public void Plan_ChangedSinceMono_LeftAlone()
    {
        var before = StereoTable("drain");
        var mono = before.MakeMono().Mono.Write();
        mono[^20] ^= 0x55; // one data byte different (the 17-byte tail follows the data)

        var item = Assert.Single(StereoRestore.Plan(One("Sound1", VpxSound.Parse(mono)), One("Sound1", before)));

        Assert.Equal((RestoreVerdict.LeftAlone, "not what the mono step made: changed another way, before or since"), (item.Verdict, item.Reason));
    }

    [Fact]
    public void Plan_DifferentStreamOrName_LeftAlone()
    {
        var before = StereoTable("drain");
        var mono = before.MakeMono().Mono;

        var moved = Assert.Single(StereoRestore.Plan(One("Sound2", mono), One("Sound1", before)));
        var renamed = Assert.Single(StereoRestore.Plan(One("Sound1", StereoTable("other").MakeMono().Mono), One("Sound1", before)));

        Assert.Equal((RestoreVerdict.LeftAlone, "not at the same place in the table"), (moved.Verdict, moved.Reason));
        Assert.Equal((RestoreVerdict.LeftAlone, "not at the same place in the table"), (renamed.Verdict, renamed.Reason));
    }

    [Fact]
    public void PlanAll_SoundChangedBeforeTheMonoStep_MatchesTheLaterBackup()
    {
        var first = StereoTable("frankhit", 100, 200);
        var second = StereoTable("frankhit", 900, 800); // changed by hand on another day, then the mono step ran
        var other = StereoTable("drain");
        var now = new List<(string, VpxSound)> { ("Sound1", second.MakeMono().Mono), ("Sound2", other.MakeMono().Mono) };

        var items = StereoRestore.PlanAll(now, [("A.bak", [("Sound1", first), ("Sound2", other)]), ("B.bak", [("Sound1", second), ("Sound2", other)])]);

        Assert.Equal([("frankhit", RestoreVerdict.Restore, "B.bak"), ("drain", RestoreVerdict.Restore, "A.bak")],
            items.Select(i => (i.Name, i.Verdict, i.Source)));
    }

    [Fact]
    public void PlanAll_NoBackupMatches_LeftAloneOnce()
    {
        var before = StereoTable("drain");
        var mono = before.MakeMono().Mono.Write();
        mono[^20] ^= 0x55;

        var item = Assert.Single(StereoRestore.PlanAll([("Sound1", VpxSound.Parse(mono))], [("A.bak", [("Sound1", before)]), ("B.bak", [("Sound1", before)])]));

        Assert.Equal((RestoreVerdict.LeftAlone, "not what the mono step made: changed another way, before or since"), (item.Verdict, item.Reason));
    }

    [Fact]
    public void Markdown_NamesTheBackupsTheStereoComesFrom()
    {
        var t = new TableStereo("M.vpx", "M.vpx.20261112-210423.bak",
            [new("S", "frankhit", SoundOutput.Table, RestoreVerdict.Restore, "", null, "M.vpx.20261113-140505.bak")]);

        var md = StereoRestore.Markdown([t]);

        Assert.Contains("Stereo from: `M.vpx.20261113-140505.bak`", md);
        Assert.DoesNotContain("20261112", md);
    }

    [Fact]
    public void Plan_NeverStereo_NotListed()
    {
        var mono = StereoTable("flip").MakeMono().Mono;

        Assert.Empty(StereoRestore.Plan(One("Sound1", mono), One("Sound1", mono)));
    }

    [Fact]
    public void Markdown_ShowsCountsFlagsAndNothingToRestore()
    {
        RestoreItem Item(string name, AudioFacts f, SoundOutput o = SoundOutput.Table) => new("S", name, o, RestoreVerdict.Restore, "", f);
        var a = new TableStereo("A.vpx", "A.vpx.20261113-140050.bak",
        [
            Item("sling", new(-3, -0.95, false)),
            Item("loud", new(-1, 0.9, true)),
            Item("sc_boom", new(-6, 0.5, false), SoundOutput.Backglass),
            new("S", "drain", SoundOutput.Table, RestoreVerdict.LeftAlone, "not what the mono step made: changed another way, before or since", null),
        ]);
        var b = new TableStereo("B.vpx", null, []);

        var md = StereoRestore.Markdown([b, a]);

        Assert.StartsWith("# Table stereo report", md);
        Assert.Contains("## A.vpx", md);
        Assert.Contains("Made mono: 3 sounds", md);
        Assert.Contains("Left alone: drain (not what the mono step made: changed another way, before or since)", md);
        Assert.Contains("Opposite phase (weak in VPX, stereo or mono): sling", md);
        Assert.Contains("Over full scale when VPX adds L+R (it played like this before the mono step; listen): loud", md);
        Assert.Contains("On the backbox, will get stereo back: sc_boom", md);
        Assert.True(md.IndexOf("## Nothing to restore") > md.IndexOf("## A.vpx"));
        Assert.Contains("B.vpx: never made mono by CabSound", md);
    }
}
