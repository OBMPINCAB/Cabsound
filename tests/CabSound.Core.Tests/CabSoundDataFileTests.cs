using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public sealed class CabSoundDataFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cabsound-test-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static CabSoundData Sample() => new(
        Calibration: new GroupLevels(-10, 10, 10, 0),
        SubConnected: false,
        SsfLimitDb: 10,
        Tables: [new TableLevel("System", 100, 66, 0, 0, 0), new TableLevel("VP.Totan", 80, 66, 20, -1, 0, SubDb: 2)]);

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);

        CabSoundDataFile.Save(path, Sample());
        var loaded = CabSoundDataFile.Load(path);

        Assert.Equal(Sample().Calibration, loaded.Calibration);
        Assert.Equal(Sample().SubConnected, loaded.SubConnected);
        Assert.Equal(Sample().SsfLimitDb, loaded.SsfLimitDb);
        Assert.Equal(Sample().Tables, loaded.Tables);
    }

    [Fact]
    public void Load_MissingFile_GivesDefaults()
    {
        var loaded = CabSoundDataFile.Load(Path.Combine(_dir, "nope.json"));

        Assert.Equal(new GroupLevels(0, 0, 0, 0), loaded.Calibration);
        Assert.False(loaded.SubConnected);
        Assert.Equal(10, loaded.SsfLimitDb);
        Assert.Equal(6, loaded.SsfBoostLimitDb);
        Assert.Equal(100, loaded.GlobalVolume);
        Assert.Equal(100, loaded.NightVolume);
        Assert.Empty(loaded.Tables);
    }

    [Fact]
    public void Load_OldFileWithoutNewLevels_GetsDefaults()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        File.WriteAllText(path, """
            { "Calibration": { "Backglass": -10, "Rear": 10, "Front": 10, "Sub": 0 },
              "SubConnected": false, "SsfLimitDb": 10, "Tables": [] }
            """);

        var loaded = CabSoundDataFile.Load(path);
        Assert.Equal(6, loaded.SsfBoostLimitDb);
        Assert.Equal(100, loaded.GlobalVolume);
        Assert.Equal(100, loaded.NightVolume);
    }

    [Fact]
    public void Save_IsReadableText_WithTableNames()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);

        CabSoundDataFile.Save(path, Sample());

        var text = File.ReadAllText(path);
        Assert.Contains("\"VP.Totan\"", text);
        Assert.DoesNotContain("DisplayOrDefault", text);
        Assert.Contains("\n", text);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsNewFields()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        var data = Sample() with
        {
            DeviceId = "{0.0.0.00000000}.{bb89ed76-1f3d-464e-8683-657bd8800e61}",
            Display = new DisplaySettings(3610, 50, 180, 2035, 90),
            SwitchOverBackup = @"C:\x\config.txt.20261110-060116.bak",
        };

        CabSoundDataFile.Save(path, data);
        var loaded = CabSoundDataFile.Load(path);

        Assert.Equal(data.DeviceId, loaded.DeviceId);
        Assert.Equal(data.DeviceName, loaded.DeviceName);
        Assert.Equal(data.Display, loaded.Display);
        Assert.Equal(data.SwitchOverBackup, loaded.SwitchOverBackup);
    }

    [Fact]
    public void Load_OldFileWithoutNewFields_UsesDefaults()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        File.WriteAllText(path, """
            { "Calibration": { "Backglass": -10, "Rear": 10, "Front": 10, "Sub": 0 },
              "SubConnected": false, "SsfLimitDb": 10, "Tables": [] }
            """);

        var loaded = CabSoundDataFile.Load(path);

        Assert.Null(loaded.DeviceId);
        Assert.Equal(1, loaded.VolumeStep);
        Assert.Equal("USB Sound Device", loaded.DeviceName);
        Assert.Equal(DisplaySettings.Default, loaded.DisplayOrDefault);
        Assert.Equal(-10, loaded.Calibration.Backglass);
    }

    [Fact]
    public void Load_CorruptFile_Throws()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        File.WriteAllText(path, "{ not json");

        var e = Assert.Throws<InvalidDataException>(() => CabSoundDataFile.Load(path));
        Assert.Contains(path, e.Message);
    }

    [Fact]
    public void Overlays_RoundTrip()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        var overlays = new OverlaySettings(
            Popup: new OverlayPlacement("DISPLAY2", 90, OverlayPosition.LockdownBar, 120, 40, 400, 120),
            Menu: new OverlayPlacement(null, 0, OverlayPosition.Middle, 0, 0, 900, 700, 120, 85));

        CabSoundDataFile.Save(path, Sample() with { Overlays = overlays });
        var loaded = CabSoundDataFile.Load(path);

        Assert.Equal(overlays, loaded.Overlays);
        Assert.Contains("\"LockdownBar\"", File.ReadAllText(path));
    }

    [Fact]
    public void OldFile_WithoutOverlays_LoadsNull()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        File.WriteAllText(path, """
            { "Calibration": { "Backglass": -10, "Rear": 10, "Front": 10, "Sub": 0 },
              "SubConnected": false, "SsfLimitDb": 10, "Tables": [],
              "Display": { "X": 3610, "Y": 50, "Width": 180, "Height": 2035, "Rotation": 90 } }
            """);

        var loaded = CabSoundDataFile.Load(path);

        Assert.Null(loaded.Overlays);
        Assert.Equal(new DisplaySettings(3610, 50, 180, 2035, 90), loaded.Display);
    }

    [Fact]
    public void OverlaySettings_GetWith()
    {
        var popup = new OverlayPlacement("DISPLAY1", 0, OverlayPosition.LockdownBar, 0, 0, 400, 120);
        var menu = new OverlayPlacement(null, 0, OverlayPosition.Middle, 0, 0, 900, 700, 100, 85);
        var settings = new OverlaySettings(popup, popup);

        var updated = settings.With(OverlayKind.Menu, menu);

        Assert.Equal(menu, updated.Get(OverlayKind.Menu));
        Assert.Equal(popup, updated.Get(OverlayKind.Popup));
    }

    [Fact]
    public void FileName_IsCabSoundJson()
    {
        Assert.Equal("CabSound.json", CabSoundDataFile.FileName);
    }

    [Fact]
    public void Load_OldFileWithoutSoundOrLowCuts_UsesDefaults()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        File.WriteAllText(path, """{"Calibration":{"Backglass":-10,"Rear":10,"Front":10,"Sub":0},"SubConnected":false,"SsfLimitDb":10,"Tables":[{"Name":"VP.X","Volume":50,"Volume2":66,"BackglassDb":1,"RearDb":2,"FrontDb":3,"SubDb":0}]}""");

        var data = CabSoundDataFile.Load(path);

        Assert.Null(data.Sound);
        Assert.Equal(new SoundSettings(), data.SoundOrDefault);
        Assert.Equal(new TableLevel("VP.X", 50, 66, 1, 2, 3), Assert.Single(data.Tables));
        Assert.Null(data.Tables[0].BackboxLowCut);
        Assert.Null(data.Tables[0].ExciterLowCut);
    }

    [Fact]
    public void Load_OldFileWithoutPresets_HasNullPresets_AndTablesOnZero()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        File.WriteAllText(path, """{"Calibration":{"Backglass":-10,"Rear":10,"Front":10,"Sub":0},"SubConnected":false,"SsfLimitDb":10,"Tables":[{"Name":"VP.X","Volume":50,"Volume2":66,"BackglassDb":1,"RearDb":2,"FrontDb":3,"SubDb":0}]}""");

        var data = CabSoundDataFile.Load(path);

        Assert.Null(data.Presets);
        Assert.Equal(0, data.Tables[0].Preset);
    }

    [Fact]
    public void LegacyPresetMigration_BakesEffectiveLevelsAndLowCuts_AndClearsPresets()
    {
        var presets = new List<LegacyPreset>
        {
            new("Default", BackboxDb: -2, FrontDb: 1, RearDb: 0, BackboxCut: 40, ExciterCut: 60),
            new("Loud", BackboxDb: 5, FrontDb: 4, RearDb: -8, BackboxCut: 90, ExciterCut: 0),
        };
        var tables = new List<TableLevel>
        {
            new("VP.OnDefault", 70, 66, BackglassDb: 1, RearDb: 0, FrontDb: 2, Preset: 0),
            new("VP.OnLoud", 60, 66, BackglassDb: 3, RearDb: -5, FrontDb: 0, BackboxLowCut: 120, Preset: 1),
        };
        var data = Sample() with { SsfBoostLimitDb = 6, Presets = presets, Tables = tables };

        var (migrated, changed) = LegacyPresetMigration.Apply(data);

        Assert.True(changed);
        Assert.Null(migrated.Presets);
        Assert.Equal(40, migrated.Sound!.BackboxCut);
        Assert.Equal(60, migrated.Sound.ExciterCut);
        Assert.Equal(new TableLevel("VP.OnDefault", 70, 66, BackglassDb: -1, RearDb: 0, FrontDb: 3), migrated.Tables[0]);
        // BackglassDb 5+3=8 clamped to +6; RearDb -8-5=-13 clamped to -10; BackboxLowCut 120 kept; ExciterLowCut 0 differs from cabinet 60 so kept
        Assert.Equal(new TableLevel("VP.OnLoud", 60, 66, BackglassDb: 6, RearDb: -10, FrontDb: 4, BackboxLowCut: 120, ExciterLowCut: 0), migrated.Tables[1]);

        // Second run is a no-op
        var (again, changedAgain) = LegacyPresetMigration.Apply(migrated);
        Assert.False(changedAgain);
        Assert.Equal(migrated, again);
    }

    [Fact]
    public void OldFile_LoadsWithoutOptionalFields()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        File.WriteAllText(path,
            """{ "Calibration": { "Backglass": -10, "Rear": 10, "Front": 10, "Sub": 0 }, "SubConnected": false, "SsfLimitDb": 10, "Tables": [] }""");

        var loaded = CabSoundDataFile.Load(path);

        Assert.Equal(new GroupLevels(-10, 10, 10, 0), loaded.Calibration);
        Assert.Null(loaded.SpeakerCheck);
        Assert.Null(loaded.ToolWindow);
    }

    [Fact]
    public void ToolWindow_RoundTrips()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        var place = new ToolWindowPlace(90, 2500, 300);

        CabSoundDataFile.Save(path, Sample() with { ToolWindow = place });

        Assert.Equal(place, CabSoundDataFile.Load(path).ToolWindow);
    }

    [Fact]
    public void SpeakerCheck_RoundTrips()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        var result = new SpeakerCheckResult("front and rear exciters are exchanged", "2026-11-12");

        CabSoundDataFile.Save(path, Sample() with { SpeakerCheck = result });

        Assert.Equal(result, CabSoundDataFile.Load(path).SpeakerCheck);
    }

    [Fact]
    public void SaveLoad_KeepsSoundAndLowCuts()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        var sound = new SoundSettings(LowCutMode.Gentle, Dip250: false, JacksSwapped: true, RattleWarned: true);
        var table = new TableLevel("VP.X", 50, 66, 1, 2, 3, BackboxLowCut: 0, ExciterLowCut: 120);

        CabSoundDataFile.Save(path, Sample() with { Sound = sound, Tables = [table] });
        var loaded = CabSoundDataFile.Load(path);

        Assert.Equal(sound, loaded.Sound);
        Assert.Equal(table, Assert.Single(loaded.Tables));
    }

    [Fact]
    public void Load_OverlaysSavedBeforeTheHintSetting_HintIsOn()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        var placement = """{"Monitor":null,"Rotation":270,"Position":"LockdownBar","Along":0,"Toward":0,"Width":400,"Height":120}""";
        File.WriteAllText(path,
            """{"Calibration":{"Backglass":0,"Rear":0,"Front":0,"Sub":0},"SubConnected":false,"SsfLimitDb":10,"Tables":[],"Overlays":{"Popup":"""
            + placement + ""","Menu":""" + placement + "}}");

        Assert.True(CabSoundDataFile.Load(path).Overlays!.ShowHint);
    }

    [Fact]
    public void SaveLoad_KeepsTheHintSwitchedOff()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        var p = new OverlayPlacement(null, 0, OverlayPosition.Middle, 0, 0, 400, 120);

        CabSoundDataFile.Save(path, Sample() with { Overlays = new OverlaySettings(p, p, ShowHint: false) });

        Assert.False(CabSoundDataFile.Load(path).Overlays!.ShowHint);
    }
}
