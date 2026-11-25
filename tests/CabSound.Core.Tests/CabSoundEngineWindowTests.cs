using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

/// <summary>Engine methods the CabSound window uses (step E).</summary>
public class CabSoundEngineWindowTests
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

        /// <summary>Card switches, each with the number of volume calls made before it (for ordering).</summary>
        public List<(string Id, string Name, int VolumesBefore)> Cards { get; } = [];
        public void UseSoundCard(string id, string name) => Cards.Add((id, name, Volumes.Count));
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
    public void SetLevels_RunningTable_AppliesVolumeAndWritesIncludeAfter300ms()
    {
        var (engine, output) = OnTableA();

        engine.SetLevels(engine.Current with { Volume = 50, FrontDb = 1 }, 0);

        Assert.Equal([50], output.Volumes);
        engine.Tick(299);
        Assert.Empty(output.Includes);
        engine.Tick(300);
        Assert.Contains("Channel: RL RR\r\n" + ExciterFilters + "Preamp: -2 dB", Assert.Single(output.Includes));
        Assert.Empty(output.Popups);
    }

    [Fact]
    public void SetLevels_OtherTable_DoesNotTouchRunningSound()
    {
        var (engine, output) = OnTableA();

        engine.SetLevels(new TableLevel("VP.B", 30, 66, 1, 1, 1), 0);
        engine.Tick(5000);

        Assert.Empty(output.Volumes);
        Assert.All(output.Includes, i => Assert.Contains("Channel: L R\r\n" + BackboxFilters + "Preamp: -21 dB", i));
        Assert.Contains(new TableLevel("VP.B", 30, 66, 1, 1, 1), Assert.Single(output.Saves).Tables);
    }

    [Fact]
    public void SetLevels_Unchanged_WritesNothing()
    {
        var (engine, output) = OnTableA();

        engine.SetLevels(engine.Current, 0);
        engine.Tick(5000);

        Assert.Equal(0, output.Calls);
    }

    [Fact]
    public void Reset_GivesSystemVolumeAndZeroOffsets()
    {
        var (engine, output) = OnTableA();

        engine.Reset("VP.A", 0);

        Assert.Equal(new TableLevel("VP.A", 100, 66, 0, 0, 0), engine.Current);
        Assert.Equal([100], output.Volumes);
    }

    [Fact]
    public void CopyLevels_ToRunningTable_AppliesNow()
    {
        var (engine, output) = OnTableA(Data(new TableLevel("VP.B", 30, 50, -3, 1, 0)));

        engine.CopyLevels("VP.B", ["VP.A", "VP.C", "VP.B"], 0);

        Assert.Equal(new TableLevel("VP.A", 30, 66, -3, 1, 0), engine.Current);
        Assert.Equal([30], output.Volumes);
        var tables = engine.Snapshot().Tables;
        Assert.Contains(new TableLevel("VP.C", 30, 66, -3, 1, 0), tables);
        Assert.Contains(new TableLevel("VP.B", 30, 50, -3, 1, 0), tables);
    }

    [Fact]
    public void SetGlobal_InNightMode_ChangesGlobalWithoutTouchingVolume()
    {
        var (engine, output) = OnTableA();
        engine.SetNightMode(true);
        var volumes = output.Volumes.Count;

        engine.SetGlobal(50, 0);

        Assert.Equal(volumes, output.Volumes.Count);
        Assert.Equal(50, engine.Snapshot().GlobalVolume);
    }

    [Fact]
    public void SetNight_WhileNightModeOn_AppliesAtOnce()
    {
        var (engine, output) = OnTableA(Data() with { NightVolume = 50 });
        engine.SetNightMode(true);

        engine.SetNight(40, 0);

        Assert.Equal(17, output.Volumes[^1]); // 43 x 0.40 = 17.2
        Assert.Equal(40, engine.Snapshot().NightVolume);
        Assert.Empty(output.Popups);
    }

    [Fact]
    public void SetNightMode_SameValue_DoesNothing()
    {
        var (engine, output) = OnTableA();

        engine.SetNightMode(false);
        engine.Tick(5000);

        Assert.Equal(0, output.Calls);
    }

    [Fact]
    public void SetCalibration_RewritesIncludeAndSaves()
    {
        var (engine, output) = OnTableA();

        engine.SetCalibration(new GroupLevels(-5, 10, 10, 0), 0);
        engine.Tick(2000);

        Assert.Contains("Channel: L R\r\n" + BackboxFilters + "Preamp: -16 dB", output.Includes[^1]); // 2 - 5 - 13
        Assert.Equal(new GroupLevels(-5, 10, 10, 0), Assert.Single(output.Saves).Calibration);
    }

    [Fact]
    public void ResetCalibration_SetsAllGroupsToZeroDb_RecoversBackboxHeadroom()
    {
        var (engine, output) = OnTableA();

        engine.ResetCalibration(0);
        engine.Tick(2000);

        Assert.Equal(new GroupLevels(0, 0, 0, 0), engine.Snapshot().Calibration);
        Assert.Equal(3, engine.State.HeadroomDb); // only boost limit 3, no +10 exciter penalty
        Assert.Contains("Channel: L R\r\n" + BackboxFilters + "Preamp: -1 dB", output.Includes[^1]); // +2 offset - 3 headroom = -1 dB (was -21 dB!)
    }

    [Fact]
    public void SetCalibration_NothingWrittenAboveZero()
    {
        var (engine, output) = OnTableA();

        engine.SetCalibration(new GroupLevels(10, 10, 10, 0), 0);
        engine.Tick(300);

        var preamps = output.Includes[^1].Split("\r\n").Where(l => l.StartsWith("Preamp:"))
            .Select(l => int.Parse(l.Split(' ')[1]));
        Assert.All(preamps, p => Assert.True(p <= 0));
    }

    [Fact]
    public void SetCalibration_ClampsToMinus30Plus10()
    {
        var (engine, _) = OnTableA();

        engine.SetCalibration(new GroupLevels(-50, 20, 0, 0), 0);

        Assert.Equal(new GroupLevels(-30, 10, 0, 0), engine.Snapshot().Calibration);
    }

    [Fact]
    public void State_ShowsRunningTableAppliedLevelsAndHeadroom()
    {
        var (engine, _) = OnTableA();

        var s = engine.State;

        Assert.Equal("VP.A", s.Table);
        Assert.Equal(engine.Current, s.Levels);
        Assert.Equal((-21, -3, -4), (s.Applied.Backglass, s.Applied.Rear, s.Applied.Front));
        Assert.Equal(13, s.HeadroomDb);
        Assert.Equal((10, 3), (s.CutLimitDb, s.BoostLimitDb));
        Assert.Equal(2, s.Tables.Count);
        Assert.False(s.CanUndo);
        Assert.False(s.NightMode);
    }

    [Fact]
    public void Undo_RestoresLevelsBeforeTheGesture()
    {
        var (engine, output) = OnTableA();

        engine.SaveUndoPoint(["VP.A"]);
        Assert.True(engine.State.CanUndo);
        engine.SetLevels(engine.Current with { FrontDb = 1 }, 0);
        engine.SetLevels(engine.Current with { FrontDb = 2 }, 0);
        engine.Undo(0);

        Assert.Equal(-1, engine.Current.FrontDb);
        Assert.False(engine.State.CanUndo);
        Assert.Equal(43, output.Volumes[^1]);
    }

    [Fact]
    public void Undo_AfterTableChange_RestoresEditedTable()
    {
        var (engine, output) = OnTableA();

        engine.SaveUndoPoint(["VP.A"]);
        engine.SetLevels(engine.Current with { Volume = 50 }, 0);
        engine.TableChanged("VP.B", 0);
        engine.Undo(0);

        Assert.Equal(43, engine.Snapshot().Tables.Single(t => t.Name == "VP.A").Volume);
        Assert.Equal(100, output.Volumes[^1]); // VP.B (new: System volume) is running
    }

    [Fact]
    public void Undo_RestoresCalibrationAndGlobal()
    {
        var (engine, _) = OnTableA();

        engine.SaveSettingsUndoPoint();
        engine.SetCalibration(new GroupLevels(-5, 10, 10, 0), 0);
        engine.SetGlobal(50, 0);
        engine.Undo(0);

        Assert.Equal(new GroupLevels(-10, 10, 10, 0), engine.Snapshot().Calibration);
        Assert.Equal(100, engine.Snapshot().GlobalVolume);
    }

    [Fact]
    public void Undo_WithoutUndoPoint_DoesNothing()
    {
        var (engine, output) = OnTableA();

        engine.Undo(0);
        engine.Tick(5000);

        Assert.Equal(0, output.Calls);
    }

    // Final review, Important 1: undo only what the window gesture touched
    [Fact]
    public void Undo_KeepsButtonChangesMadeElsewhereAfterTheUndoPoint()
    {
        var (engine, _) = OnTableA();

        engine.SaveUndoPoint(["VP.A"]);
        engine.SetTableValue("VP.A", LevelField.Front, 1, 0);
        engine.TableChanged("VP.B", 0);
        engine.Handle(CabAction.TableDown, 0);   // cabinet button on another table
        engine.Handle(CabAction.GlobalDown, 0);
        engine.SetVolumeStep(5, 0);              // tray
        engine.Undo(0);

        var snap = engine.Snapshot();
        Assert.Equal(-1, snap.Tables.Single(t => t.Name == "VP.A").FrontDb);
        Assert.Equal(99, snap.Tables.Single(t => t.Name == "VP.B").Volume);
        Assert.Equal(99, snap.GlobalVolume);
        Assert.Equal(5, snap.VolumeStep);
    }

    [Fact]
    public void Undo_TableThatWasNew_IsRemovedAgain()
    {
        var (engine, _) = OnTableA();

        engine.SaveUndoPoint(["VP.C"]);
        engine.SetTableValue("VP.C", LevelField.Volume, 30, 0);
        engine.Undo(0);

        Assert.DoesNotContain(engine.Snapshot().Tables, t => t.Name == "VP.C");
    }

    [Fact]
    public void SettingsUndo_LeavesTablesAlone()
    {
        var (engine, _) = OnTableA();

        engine.SaveSettingsUndoPoint();
        engine.SetCalibrationGroup(SsfGroup.Backglass, -5, 0);
        engine.Handle(CabAction.FrontDown, 0);
        engine.Undo(0);

        Assert.Equal(-10, engine.Snapshot().Calibration.Backglass);
        Assert.Equal(-2, engine.Current.FrontDb);
    }

    // Final review, Important 2: window edits change one field, resolved on the engine thread
    [Fact]
    public void SetTableValue_KeepsFieldsChangedMeanwhile()
    {
        var (engine, _) = OnTableA();

        engine.SetTableValue("VP.A", LevelField.Front, 1, 0);
        engine.Handle(CabAction.TableUp, 0);     // button press during a slider drag
        engine.SetTableValue("VP.A", LevelField.Front, 2, 0);

        Assert.Equal(new TableLevel("VP.A", 44, 66, 2, 0, 2), engine.Current);
    }

    [Theory]
    [InlineData(LevelField.Volume, 50)]
    [InlineData(LevelField.Backglass, -3)]
    [InlineData(LevelField.Rear, 1)]
    public void SetTableValue_SetsTheNamedField(LevelField field, int value)
    {
        var (engine, _) = OnTableA();

        engine.SetTableValue("VP.A", field, value, 0);

        var t = engine.Current;
        Assert.Equal(value, field switch { LevelField.Volume => t.Volume, LevelField.Backglass => t.BackglassDb, _ => t.RearDb });
    }

    [Fact]
    public void SetCalibrationGroup_KeepsOtherGroups()
    {
        var (engine, _) = OnTableA();

        engine.SetCalibrationGroup(SsfGroup.Backglass, -5, 0);
        engine.SetCalibrationGroup(SsfGroup.Front, 5, 0);

        Assert.Equal(new GroupLevels(-5, 10, 5, 0), engine.Snapshot().Calibration);
    }

    [Fact]
    public void SetOverlay_Saves_AndShowsInState()
    {
        var (engine, output) = OnTableA();
        var menu = engine.State.Overlays.Menu with { Along = 40, SizePercent = 120 };

        engine.SetOverlay(OverlayKind.Menu, menu, 0);

        Assert.Equal(menu, engine.State.Overlays.Menu);
        engine.Tick(1999);
        Assert.Empty(output.Saves);
        engine.Tick(2000);
        Assert.Equal(menu, Assert.Single(output.Saves).Overlays!.Menu);
    }

    [Fact]
    public void SetOverlay_Same_DoesNothing()
    {
        var (engine, output) = OnTableA();

        engine.SetOverlay(OverlayKind.Popup, engine.State.Overlays.Popup, 0);
        engine.Tick(5000);

        Assert.Equal(0, output.Calls);
    }

    // --- Low cut per table and cabinet sound settings ---

    [Fact]
    public void SetLowCut_RunningTable_RewritesTheFileAndSaves()
    {
        var (engine, output) = OnTableA();

        engine.SetLowCut("VP.A", LowCutGroup.Exciters, 120, 0);
        engine.Tick(300);
        engine.Tick(2000);

        var file = Assert.Single(output.Includes);
        Assert.Contains("Channel: SL SR\r\nFilter: ON HP Fc 120 Hz\r\n", file);
        Assert.Contains("Channel: RL RR\r\nFilter: ON HP Fc 120 Hz\r\n", file);
        Assert.Contains("Channel: L R\r\n" + BackboxFilters, file); // backbox still on the cabinet default
        Assert.Equal(120, Assert.Single(output.Saves).Tables.Single(t => t.Name == "VP.A").ExciterLowCut);
    }

    [Fact]
    public void SetLowCut_RearExciters_ChangesOnlySlSr()
    {
        var (engine, output) = OnTableA();

        engine.SetLowCut("VP.A", LowCutGroup.Exciters, 40, 0);
        engine.SetLowCut("VP.A", LowCutGroup.RearExciters, 90, 0);
        engine.Tick(300);

        var file = Assert.Single(output.Includes);
        Assert.Contains("Channel: RL RR\r\nFilter: ON HP Fc 40 Hz\r\n", file);
        Assert.Contains("Channel: SL SR\r\nFilter: ON HP Fc 90 Hz\r\n", file);
        Assert.Equal((40, 90), (engine.Current.ExciterLowCut, engine.Current.RearExciterLowCut));
    }

    [Fact]
    public void SetLowCut_Backbox_Off_WritesNoBackboxCut()
    {
        var (engine, output) = OnTableA();

        engine.SetLowCut("VP.A", LowCutGroup.Backbox, 0, 0);
        engine.Tick(300);

        Assert.Contains("Channel: L R\r\nPreamp:", output.Includes[^1]);
        Assert.Equal(0, engine.Current.BackboxLowCut);
    }

    [Fact]
    public void SetLowCut_OtherTable_SavesButDoesNotRewriteTheFile()
    {
        var (engine, output) = OnTableA(Data(new TableLevel("VP.B", 50, 66, 0, 0, 0)));

        engine.SetLowCut("VP.B", LowCutGroup.Exciters, 60, 0);
        engine.Tick(300);
        engine.Tick(2000);

        Assert.Empty(output.Includes);
        Assert.Equal(60, Assert.Single(output.Saves).Tables.Single(t => t.Name == "VP.B").ExciterLowCut);
    }

    [Fact]
    public void SetLowCut_SameValue_DoesNothing()
    {
        var (engine, output) = OnTableA();

        engine.SetLowCut("VP.A", LowCutGroup.Exciters, null, 0);
        engine.Tick(5000);

        Assert.Equal(0, output.Calls);
    }

    [Fact]
    public void SetSound_DipOff_RewritesTheFileAndSaves()
    {
        var (engine, output) = OnTableA();

        engine.SetSound(new SoundSettings(Dip250: false), 0);
        engine.Tick(300);
        engine.Tick(2000);

        Assert.DoesNotContain("PK", output.Includes[^1]);
        Assert.False(Assert.Single(output.Saves).Sound!.Dip250);
        Assert.False(engine.State.Sound.Dip250);
    }

    private static CabSoundData OldCard() =>
        Data() with { DeviceId = "old", DeviceName = "USB Sound Device", DeviceDriver = "Microsoft 10.0" };

    [Fact]
    public void SetSoundCard_SwitchesTheOutputFirst_ThenAppliesTheTableVolume()
    {
        var (engine, output) = OnTableA(OldCard());

        engine.SetSoundCard("new", "USB Multi-Channel Audio Device", 0);

        Assert.Equal([("new", "USB Multi-Channel Audio Device", 0)], output.Cards);
        Assert.Equal([43], output.Volumes);
    }

    [Fact]
    public void SetSoundCard_IsSaved_AndTheOldCardsDriverIsForgotten()
    {
        var (engine, output) = OnTableA(OldCard());

        engine.SetSoundCard("new", "USB Multi-Channel Audio Device", 0);
        engine.Tick(2000);

        var saved = Assert.Single(output.Saves);
        Assert.Equal("new", saved.DeviceId);
        Assert.Equal("USB Multi-Channel Audio Device", saved.DeviceName);
        Assert.Null(saved.DeviceDriver); // next start just remembers the new card's driver, no warning
        Assert.Equal("new", engine.State.SoundCardId);
        Assert.Empty(output.Includes);   // CabSound.txt doesn't depend on the card
    }

    [Fact]
    public void SetSoundCard_SameCard_DoesNothing()
    {
        var (engine, output) = OnTableA(OldCard());

        engine.SetSoundCard("old", "USB Sound Device", 0);
        engine.Tick(5000);

        Assert.Equal(0, output.Calls);
        Assert.Empty(output.Cards);
        Assert.Equal("old", engine.State.SoundCardId);
    }

    [Fact]
    public void SetSound_Unchanged_DoesNothing()
    {
        var (engine, output) = OnTableA();

        engine.SetSound(new SoundSettings(), 0);
        engine.Tick(5000);

        Assert.Equal(0, output.Calls);
    }

    [Fact]
    public void State_Sound_IsTheDefaultForOldData()
    {
        var (engine, _) = OnTableA();

        Assert.Equal(new SoundSettings(), engine.State.Sound);
    }

    [Fact]
    public void Reset_ClearsLowCuts()
    {
        var (engine, _) = OnTableA();
        engine.SetLowCut("VP.A", LowCutGroup.Backbox, 40, 0);
        engine.SetLowCut("VP.A", LowCutGroup.Exciters, 0, 0);

        engine.Reset("VP.A", 0);

        Assert.Null(engine.Current.BackboxLowCut);
        Assert.Null(engine.Current.ExciterLowCut);
    }

    [Fact]
    public void CopyLevels_CopiesLowCuts()
    {
        var (engine, _) = OnTableA(Data(new TableLevel("VP.B", 50, 66, 0, 0, 0)));
        engine.SetLowCut("VP.A", LowCutGroup.Backbox, 40, 0);
        engine.SetLowCut("VP.A", LowCutGroup.Exciters, 120, 0);

        engine.CopyLevels("VP.A", ["VP.B"], 0);

        var b = engine.State.Tables.Single(t => t.Name == "VP.B");
        Assert.Equal((40, 120), (b.BackboxLowCut, b.ExciterLowCut));
    }

    [Fact]
    public void Undo_RestoresALowCut()
    {
        var (engine, _) = OnTableA();
        engine.SaveUndoPoint(["VP.A"]);
        engine.SetLowCut("VP.A", LowCutGroup.Exciters, 0, 0);

        engine.Undo(0);

        Assert.Null(engine.Current.ExciterLowCut);
    }

    [Fact]
    public void Undo_RestoresSoundSettings()
    {
        var (engine, output) = OnTableA();
        engine.SaveSettingsUndoPoint();
        engine.SetSound(new SoundSettings(LowCutMode.Gentle, JacksSwapped: true), 0);

        engine.Undo(0);
        engine.Tick(300);

        Assert.Equal(new SoundSettings(), engine.State.Sound);
        Assert.DoesNotContain("Copy:", output.Includes[^1]);
    }

    // --- Shortcut hint on the pop-up ---

    [Fact]
    public void ShowHint_IsOnByDefault()
    {
        var (engine, _) = OnTableA();

        Assert.True(engine.State.Overlays.ShowHint);
    }

    [Fact]
    public void SetShowHint_Off_SavesAndShowsInState_WithoutRewritingTheSoundFile()
    {
        var (engine, output) = OnTableA();

        engine.SetShowHint(false, 0);
        engine.Tick(2000);

        Assert.False(engine.State.Overlays.ShowHint);
        Assert.False(Assert.Single(output.Saves).Overlays!.ShowHint);
        Assert.Empty(output.Includes);
    }

    [Fact]
    public void SetShowHint_Same_DoesNothing()
    {
        var (engine, output) = OnTableA();

        engine.SetShowHint(true, 0);
        engine.Tick(5000);

        Assert.Equal(0, output.Calls);
    }

    [Fact]
    public void SetOverlay_KeepsTheHintSetting()
    {
        var (engine, _) = OnTableA();
        engine.SetShowHint(false, 0);

        engine.SetOverlay(OverlayKind.Popup, engine.State.Overlays.Popup with { Along = 30 }, 0);

        Assert.False(engine.State.Overlays.ShowHint);
    }
}
