using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class TableSoundReportTests
{
    private static TableScan Scanned(string file, params SoundFinding[] f) => TableScan.Scanned(file, f, sounds: 10, rom: null, sidecar: false, changed: "");

    [Theory]
    [InlineData("LoadVPM \"01560000\", \"S11.VBS\", 3.10\r\nConst cGameName = \"bttf_a28\"", "bttf_a28")]
    [InlineData("Const cGameName=\"tz_94h\" ' rom\r\nController.GameName = cGameName", "tz_94h")]
    [InlineData("Const cGameName = \"b66_orig\"\r\nExecuteGlobal GetTextFile(\"controller.vbs\")", null)] // original code, no ROM
    public void Rom_OnlyWhenScriptRunsPinMame(string script, string? expected)
    {
        Assert.Equal(expected, TableSoundReport.Rom(script));
    }

    [Fact]
    public void ChangedBefore_FromBackupsAndSidecarHeader()
    {
        string[] backups = ["Batman66_1.1.0.vpx.20261113-101542-before-mono.bak", "Other.vpx.20261112-1.bak", "Batman66_1.1.0.vbs.20261114-090000.bak"];

        Assert.Equal("CabSound sidecar; backups 2026-11-13, 2026-11-14",
            TableSoundReport.ChangedBefore("Batman66_1.1.0.vpx", backups, "' CabSound sidecar 2026-11-13: ...\r\nOption Explicit"));
        Assert.Equal("", TableSoundReport.ChangedBefore("Batman66_1.1.0.vpx", ["Other.vpx.20261112-1.bak"], null));
        Assert.Equal("", TableSoundReport.ChangedBefore("Batman66_1.1.0.vpx", [], "' table author's own sidecar"));
    }

    [Fact]
    public void Summary_ListsEveryTable_WorstFirst_ThenClean_ThenNotScanned()
    {
        var tables = new[]
        {
            TableScan.NotScanned("Future.fpt", "Future Pinball table"),
            Scanned("Clean.vpx"),
            Scanned("Bad.vpx", new(FindingKind.SilentAudio, "a"), new(FindingKind.RollClips, "b"), new(FindingKind.MissingSound, "x", 5)),
            Scanned("Worse.vpx", new(FindingKind.HitVolumeAdded, "", 1), new(FindingKind.HitVolumeAdded, "", 2), new(FindingKind.NoFrontRear, ""), new(FindingKind.UnplacedSound, "k", 3)),
        };

        var lines = TableSoundReport.Summary(tables).Split(Environment.NewLine);

        var order = lines.Where(l => l.Contains(".vpx") || l.Contains(".fpt")).Select(l => l.Split('|')[0].Trim()).ToList();
        Assert.Equal(["Worse.vpx", "Bad.vpx", "Clean.vpx", "Future.fpt"], order);
        Assert.Contains(lines, l => l.StartsWith("4 tables: 3 scanned (2 with issues), 1 not scanned"));
        Assert.Contains(lines, l => l.Contains("Future.fpt") && l.Contains("NOT SCANNED: Future Pinball table"));
        Assert.Contains(lines, l => l.Contains("Bad.vpx") && l.Contains("silent 1") && l.Contains("roll-clips 1") && l.Contains("missing 1"));
        Assert.DoesNotContain(lines, l => l.Contains("stereo"));
    }

    [Fact]
    public void Detail_GroupsByKind_WithLinesAndFixes()
    {
        var t = TableScan.Scanned("Bad.vpx",
            [new(FindingKind.OppositePhase, "sling"), new(FindingKind.OppositePhase, "rubber"),
             new(FindingKind.MissingSound, "fx_plunger", 412, "PlaySoundAt \"fx_plunger\", Plunger")],
            sounds: 40, rom: "tz_94h", sidecar: true, changed: "");

        var text = TableSoundReport.Detail(t);

        Assert.Contains("Bad.vpx", text);
        Assert.Contains("script: sidecar .vbs", text);
        Assert.Contains("ROM: tz_94h", text);
        Assert.Contains("Stereo sounds that cancel out in VPX (L+R) (2): sling, rubber", text);
        Assert.Contains("line 412 (\"fx_plunger\"): PlaySoundAt \"fx_plunger\", Plunger", text);
        Assert.Contains("Fix:", text);
    }

    [Fact]
    public void Detail_NamesTheSoundOnEachLine_SoATwoSoundLineSaysWhichOne()
    {
        const string code = "bsTrough.InitExitSnd SoundFX(\"ballrelease1\",DOFContactors), SoundFX(\"Solenoid\",DOFContactors)";
        var t = TableScan.Scanned("Tron.vpx",
            [new(FindingKind.MissingSound, "Solenoid", 159, code), new(FindingKind.MissingSound, "Bumpers_Left_*", 2003, "x"),
             new(FindingKind.HitVolumeAdded, "", 7, "Vol(ActiveBall)+VolTable")],
            sounds: 10, rom: null, sidecar: false, changed: "");

        var text = TableSoundReport.Detail(t);

        Assert.Contains($"line 159 (\"Solenoid\"): {code}", text);
        Assert.Contains("line 2003 (\"Bumpers_Left_*\"): x", text);
        Assert.Contains("line 7: Vol(ActiveBall)+VolTable", text);
    }

    [Fact]
    public void Detail_NoFindings_SaysNothingToDo()
    {
        Assert.Contains("Nothing to do", TableSoundReport.Detail(Scanned("Clean.vpx")));
    }
}
