using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class CabSoundEngineTests
{
    private const string BackboxFilters = "Filter: ON HP Fc 90 Hz\r\nFilter: ON HP Fc 90 Hz\r\n";
    private const string ExciterFilters = "Filter: ON HP Fc 90 Hz\r\nFilter: ON HP Fc 90 Hz\r\nFilter: ON PK Fc 250 Hz Gain -3.5 dB Q 1.0\r\nFilter: ON PK Fc 2800 Hz Gain 2.5 dB Q 1.0\r\n";

    private sealed class FakeOutput : IEngineOutput
    {
        public List<int> Volumes { get; } = [];
        public List<string> Includes { get; } = [];
        public List<CabSoundData> Saves { get; } = [];
        public List<PopupInfo> Popups { get; } = [];
        public int Calls => Volumes.Count + Includes.Count + Saves.Count + Popups.Count;

        public void SetVolume(int percent) => Volumes.Add(percent);
        public void WriteInclude(string text) => Includes.Add(text);
        public void SaveData(CabSoundData data) => Saves.Add(data);
        public void ShowPopup(PopupInfo info) => Popups.Add(info);
    }

    private static CabSoundData Data(params TableLevel[] extra) => new(
        new GroupLevels(-10, 10, 10, 0), false, 10,
        [new TableLevel("System", 100, 66, 0, 0, 0), new TableLevel("VP.A", 43, 66, 2, 0, -1), .. extra],
        SsfBoostLimitDb: 3);

    private static (CabSoundEngine Engine, FakeOutput Out) OnTableA(CabSoundData? data = null)
    {
        var output = new FakeOutput();
        var engine = new CabSoundEngine(data ?? Data(), output);
        engine.TableChanged("VP.A", 0);
        output.Volumes.Clear();
        output.Includes.Clear();
        return (engine, output);
    }

    [Fact]
    public void TableChanged_SetsVolumeAndWritesIncludeAtOnce()
    {
        var output = new FakeOutput();
        var engine = new CabSoundEngine(Data(), output);

        engine.TableChanged("VP.A", 0);

        Assert.Equal([43], output.Volumes);
        // backbox total -8; fixed headroom = calibration 10 + boost limit 3 = 13
        Assert.Contains("Channel: L R\r\n" + BackboxFilters + "Preamp: -21 dB", Assert.Single(output.Includes));
        Assert.Equal("VP.A", engine.CurrentTable);
    }

    [Fact]
    public void TableUp_SetsVolumeAtOnce_AndShowsPopup()
    {
        var (engine, output) = OnTableA();

        engine.Handle(CabAction.TableUp, 10);

        Assert.Equal([44], output.Volumes);
        Assert.Equal(new PopupInfo("VP.A", "Table volume", "44", false, 0.44), Assert.Single(output.Popups));
    }

    [Fact]
    public void BackglassUp_WritesIncludeOnlyAfterDelay()
    {
        var (engine, output) = OnTableA();

        engine.Handle(CabAction.BackglassUp, 0);
        engine.Tick(299);
        Assert.Empty(output.Includes);

        engine.Tick(300);
        Assert.Single(output.Includes);
        Assert.Equal(new PopupInfo("VP.A", "Backbox", "+3 dB", true, 1.0), Assert.Single(output.Popups));
    }

    [Fact]
    public void FrontButtons_AdjustTheLockbarPair()
    {
        var (engine, output) = OnTableA();

        engine.Handle(CabAction.FrontDown, 0);

        Assert.Equal(new PopupInfo("VP.A", "Front exciters", "-2 dB", false, 8.0 / 13), Assert.Single(output.Popups));
        var t = engine.Current;
        Assert.Equal(-2, t.FrontDb);
        Assert.Equal(0, t.RearDb);   // untouched
    }

    [Fact]
    public void RearButtons_AdjustTheBackboxPair()
    {
        var (engine, output) = OnTableA();

        engine.Handle(CabAction.RearDown, 0);
        engine.Handle(CabAction.RearDown, 0);
        engine.Handle(CabAction.RearUp, 0);

        Assert.Equal(new PopupInfo("VP.A", "Rear exciters", "-1 dB", false, 9.0 / 13), output.Popups[^1]);
        var t = engine.Current;
        Assert.Equal(-1, t.RearDb);
        Assert.Equal(-1, t.FrontDb); // untouched
    }

    [Fact]
    public void Changes_SaveDataAfterTwoSeconds()
    {
        var (engine, output) = OnTableA();

        engine.Handle(CabAction.FrontDown, 0);
        engine.Tick(1999);
        Assert.Empty(output.Saves);

        engine.Tick(2000);
        var saved = Assert.Single(output.Saves);
        Assert.Equal(-2, saved.Tables.Single(t => t.Name == "VP.A").FrontDb);
        Assert.Equal(new GroupLevels(-10, 10, 10, 0), saved.Calibration);
    }

    [Fact]
    public void AtLimit_FlagsPopup()
    {
        var (engine, output) = OnTableA(Data(new TableLevel("VP.Loud", 50, 66, 10, 0, 0)));
        engine.TableChanged("VP.Loud", 0);

        engine.Handle(CabAction.BackglassUp, 0);

        // An old adjustment beyond the limit (+10, limit +3): the pop-up shows what is really written
        Assert.Equal(new PopupInfo("VP.Loud", "Backbox", "+3 dB", true, 1.0), Assert.Single(output.Popups));
    }

    [Fact]
    public void NewTable_IsSavedAfterFirstChange()
    {
        var output = new FakeOutput();
        var engine = new CabSoundEngine(Data(), output);

        engine.TableChanged("VP.New", 0);
        engine.Handle(CabAction.TableDown, 0);
        engine.Flush();

        Assert.Equal([100, 99], output.Volumes);
        Assert.Equal(new TableLevel("VP.New", 99, 66, 0, 0, 0), output.Saves.Single().Tables.Single(t => t.Name == "VP.New"));
    }

    [Fact]
    public void Flush_WritesPendingIncludeAndData()
    {
        var (engine, output) = OnTableA();
        engine.Handle(CabAction.BackglassUp, 0);

        engine.Flush();
        engine.Tick(10_000);

        Assert.Single(output.Includes);
        Assert.Single(output.Saves);
    }

    [Fact]
    public void Flush_SavesEvenWhenNothingIsPending_SoAnEarlierFailedWriteIsRepaired()
    {
        // A save that failed earlier was already taken off the schedule; exit must still save
        var (engine, output) = OnTableA();
        engine.Handle(CabAction.TableUp, 0);
        engine.Tick(5000);
        output.Includes.Clear();
        output.Saves.Clear();

        engine.Flush();

        Assert.Single(output.Includes);
        Assert.Equal(44, Assert.Single(output.Saves).Tables.Single(t => t.Name == "VP.A").Volume);
    }

    [Fact]
    public void UnhandledActions_DoNothing()
    {
        var (engine, output) = OnTableA();

        engine.Handle(CabAction.OpenMenu, 0);
        engine.Handle(CabAction.Mute, 0);
        engine.Tick(10_000);

        Assert.Equal(0, output.Calls);
    }

    [Fact]
    public void VolumeStep_FromData_SetsStepSize()
    {
        var (engine, output) = OnTableA(Data() with { VolumeStep = 5 });

        engine.Handle(CabAction.TableUp, 0);

        Assert.Equal([48], output.Volumes);
    }

    [Fact]
    public void SetVolumeStep_ChangesStepAndIsSaved()
    {
        var (engine, output) = OnTableA();

        engine.SetVolumeStep(2, 0);
        engine.Handle(CabAction.TableDown, 0);
        engine.Flush();

        Assert.Equal([41], output.Volumes);
        Assert.Equal(2, output.Saves.Last().VolumeStep);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 10)]
    public void SetVolumeStep_StaysBetween1And10(int asked, int expected)
    {
        var (engine, _) = OnTableA();

        engine.SetVolumeStep(asked, 0);

        Assert.Equal(expected, engine.Snapshot().VolumeStep);
    }

    [Fact]
    public void Popup_HasLevelBarFraction()
    {
        var (engine, output) = OnTableA();

        engine.Handle(CabAction.TableUp, 0);      // 44 of 100
        engine.Handle(CabAction.BackglassDown, 0); // +1 dB of -10..+3

        Assert.Equal(0.44, output.Popups[0].Level, 3);
        Assert.Equal(11.0 / 13, output.Popups[1].Level, 3);
    }

    [Fact]
    public void OldAdjustmentBeyondTheLimit_TheFirstPressBackIsHeard()
    {
        var (engine, output) = OnTableA(Data(new TableLevel("VP.Totan", 50, 66, 20, 0, 0)));
        engine.TableChanged("VP.Totan", 0);

        engine.Handle(CabAction.BackglassDown, 0); // stored +20, written +3 (the limit): one press down must be heard

        Assert.Equal("+2 dB", output.Popups.Single().Value);
        Assert.Equal(2, engine.Current.BackglassDb);
        Assert.Equal(12 / 13.0, output.Popups.Single().Level, 3);
    }

    [Fact]
    public void AtLimit_DoesNotScheduleRedundantWrites()
    {
        var (engine, output) = OnTableA(Data(new TableLevel("VP.AtMax", 100, 66, 3, 0, 0)));
        engine.TableChanged("VP.AtMax", 0);
        output.Includes.Clear();
        output.Saves.Clear();

        engine.Handle(CabAction.BackglassUp, 10);
        engine.Handle(CabAction.TableUp, 10);
        engine.Tick(10_000);

        Assert.Empty(output.Includes);
        Assert.Empty(output.Saves);
    }

    [Fact]
    public void VolumeOnlyEdit_DoesNotRewriteIncludeFile()
    {
        var (engine, output) = OnTableA();

        engine.SetTableValue("VP.A", LevelField.Volume, 55, 10);
        engine.Tick(10_000);

        Assert.Equal([55], output.Volumes);
        Assert.Empty(output.Includes);
        Assert.Single(output.Saves);
    }

    [Fact]
    public void Current_IsTheRunningTablesLevels_EvenWhenNew()
    {
        var (engine, _) = OnTableA();
        Assert.Equal(new TableLevel("VP.A", 43, 66, 2, 0, -1), engine.Current);

        engine.TableChanged("VP.New", 0);
        Assert.Equal(new TableLevel("VP.New", 100, 66, 0, 0, 0), engine.Current);
    }

    [Fact]
    public void Snapshot_KeepsSettingsAndCurrentTables()
    {
        var data = Data() with { DeviceId = "dev", SwitchOverBackup = "bak" };
        var (engine, _) = OnTableA(data);
        engine.Handle(CabAction.TableUp, 0);

        var snap = engine.Snapshot();

        Assert.Equal("dev", snap.DeviceId);
        Assert.Equal("bak", snap.SwitchOverBackup);
        Assert.Equal(44, snap.Tables.Single(t => t.Name == "VP.A").Volume);
    }

    // Global volume (PinVol "Global") scales every table: Windows volume = table x global / 100
    [Fact]
    public void GlobalVolume_ScalesTheTableVolume()
    {
        var output = new FakeOutput();
        var engine = new CabSoundEngine(Data() with { GlobalVolume = 80 }, output);

        engine.TableChanged("VP.A", 0);

        Assert.Equal([34], output.Volumes); // 43 x 0.80 = 34.4
    }

    [Fact]
    public void GlobalUp_ChangesGlobal_AppliesAndSaves()
    {
        var (engine, output) = OnTableA(Data() with { GlobalVolume = 80 });

        engine.Handle(CabAction.GlobalUp, 0);
        engine.Tick(2000);

        Assert.Equal([35], output.Volumes); // 43 x 0.81 = 34.8
        Assert.Equal(new PopupInfo("VP.A", "Global volume", "81", false, 0.81), Assert.Single(output.Popups));
        Assert.Equal(81, Assert.Single(output.Saves).GlobalVolume);
        Assert.Equal(43, engine.Current.Volume); // table level untouched
    }

    [Fact]
    public void GlobalVolume_StaysWithin0And100()
    {
        var (engine, _) = OnTableA();

        engine.Handle(CabAction.GlobalUp, 0);

        Assert.Equal(100, engine.Snapshot().GlobalVolume);
    }

    [Fact]
    public void TableUp_UsesGlobalToo()
    {
        var (engine, output) = OnTableA(Data() with { GlobalVolume = 50 });

        engine.Handle(CabAction.TableUp, 0);

        Assert.Equal([22], output.Volumes); // 44 x 0.50
        Assert.Equal("44", output.Popups.Single().Value);
    }

    [Fact]
    public void NightMode_SwitchesToTheNightLevel_WritesNightEq_AndBack()
    {
        var (engine, output) = OnTableA(Data() with { GlobalVolume = 80, NightVolume = 50 });

        engine.Handle(CabAction.NightMode, 0);
        Assert.Equal(new PopupInfo("VP.A", "Night mode", "On", false, 0.5), output.Popups[^1]);
        Assert.Contains("Channel: L R\r\nFilter: ON HP Fc 120 Hz\r\nPreamp: -21 dB", output.Includes[^1]);
        Assert.Contains("Channel: RL RR\r\nFilter: ON HP Fc 120 Hz\r\n", output.Includes[^1]);
        Assert.Contains("Preamp: -7 dB", output.Includes[^1]); // front was -4 dB, night mode lowers exciters by 3 dB

        engine.Handle(CabAction.NightMode, 0);
        Assert.Equal(new PopupInfo("VP.A", "Night mode", "Off", false, 0.8), output.Popups[^1]);
        Assert.Contains("Channel: L R\r\n" + BackboxFilters + "Preamp: -21 dB", output.Includes[^1]);

        Assert.Equal([22, 34], output.Volumes); // 43 x 0.50 = 21.5, 43 x 0.80
    }

    [Fact]
    public void GlobalKeys_InNightMode_ChangeTheNightLevel()
    {
        var (engine, output) = OnTableA(Data() with { GlobalVolume = 80, NightVolume = 50 });

        engine.Handle(CabAction.NightMode, 0);
        engine.Handle(CabAction.GlobalDown, 0);

        Assert.Equal(new PopupInfo("VP.A", "Night volume", "49", false, 0.49), output.Popups[^1]);
        Assert.Equal(49, engine.Snapshot().NightVolume);
        Assert.Equal(80, engine.Snapshot().GlobalVolume);
    }

    [Fact]
    public void NightMode_StaysOnAcrossTableChanges()
    {
        var (engine, output) = OnTableA(Data() with { NightVolume = 50 });

        engine.Handle(CabAction.NightMode, 0);
        engine.TableChanged("VP.A", 0);

        Assert.Equal(22, output.Volumes[^1]);
    }
}
