using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

/// <summary>Speaker check / test mode in the engine.</summary>
public class CabSoundEngineCalibrationTests
{
    private const string Table = "VP.Some Table";

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

    /// <summary>Calibration backbox -12, exciters 0; boost limit 6. System volume 80, the table's 40; global 50.</summary>
    private static CabSoundData Data() =>
        new(new GroupLevels(-12, 0, 0, 0), false, 10,
            [new TableLevel("System", 80, 66, 0, 0, 0), new TableLevel(Table, 40, 66, 2, 0, -1)])
        {
            SsfBoostLimitDb = 6, GlobalVolume = 50,
        };

    private static (CabSoundEngine Engine, FakeOutput Out) Engine(CabSoundData? data = null)
    {
        var output = new FakeOutput();
        return (new CabSoundEngine(data ?? Data(), output), output);
    }

    [Fact]
    public void State_TellsWhetherATableRuns_AndTheSoundCardsName()
    {
        var (engine, _) = Engine(Data() with { DeviceId = "id-1", DeviceName = "USB Multi-Channel Audio Device" });

        Assert.False(engine.State.TableRunning);
        Assert.Equal("USB Multi-Channel Audio Device", engine.State.SoundCardName);

        engine.TableChanged("PinUP Popper", 0);
        Assert.False(engine.State.TableRunning);

        engine.TableChanged(Table, 0);
        Assert.True(engine.State.TableRunning);

        engine.SetSoundCard("id-2", "Other Card", 0);
        Assert.Equal("Other Card", engine.State.SoundCardName);
    }

    private static readonly SoundSettings Sound = new();
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly GroupLevels Flat = new(-6, -6, -6, 0); // trial 0 / 0 / 0 minus the boost limit

    private static (string Include, int Volume) Normal(string key, CabSoundData? data = null)
    {
        var (engine, output) = Engine(data);
        engine.TableChanged(key, 0);
        return (output.Includes[^1], output.Volumes[^1]);
    }

    private static (CabSoundEngine Engine, FakeOutput Out) Calibrating(CabSoundData? data = null)
    {
        var (engine, output) = Engine(data);
        Assert.True(engine.BeginCalibration());
        return (engine, output);
    }

    [Fact]
    public void Begin_RefusedWhileATableRuns()
    {
        var (engine, output) = Engine();
        engine.TableChanged(Table, 0);
        var calls = output.Calls;

        Assert.True(engine.TableRunning);
        Assert.False(engine.BeginCalibration());
        Assert.False(engine.Calibrating);
        Assert.Equal(calls, output.Calls);
    }

    [Theory]
    [InlineData("System")]
    [InlineData("PinUP Popper")]
    public void Begin_AllowedWithoutATable(string key)
    {
        var (engine, _) = Engine();
        engine.TableChanged(key, 0);

        Assert.False(engine.TableRunning);
        Assert.True(engine.BeginCalibration());
        Assert.True(engine.Calibrating);
    }

    [Fact]
    public void Begin_WritesFlatFileAndVolume10()
    {
        var (_, output) = Calibrating();

        Assert.Equal(EqApoFile.BuildCalibration(Flat, null, Sound), output.Includes[^1]);
        Assert.Equal(10, output.Volumes[^1]);
    }

    [Fact]
    public void Trial_IsWrittenAtOnce()
    {
        var (engine, output) = Calibrating();

        engine.SetCalibrationTrial(new GroupLevels(Backglass: -3, Rear: -5, Front: 0, Sub: 0), 60);

        Assert.Equal(EqApoFile.BuildCalibration(new GroupLevels(Backglass: -9, Rear: -11, Front: -6, Sub: 0), 60, Sound), output.Includes[^1]);
        Assert.Contains("Channel: RL RR\r\nFilter: ON HP Fc 60 Hz\r\n", output.Includes[^1]);
        Assert.Contains("Channel: SL SR\r\nFilter: ON HP Fc 60 Hz\r\n", output.Includes[^1]);
    }

    [Fact]
    public void Trial_KeepsTheCabinetsDipAndJackSwap()
    {
        var sound = new SoundSettings(Dip250: false, JacksSwapped: true);
        var (_, output) = Calibrating(Data() with { Sound = sound });

        Assert.Equal(EqApoFile.BuildCalibration(Flat, null, sound), output.Includes[^1]);
    }

    [Fact]
    public void Trial_WithoutBegin_DoesNothing()
    {
        var (engine, output) = Engine();

        engine.SetCalibrationTrial(new GroupLevels(-3, -5, 0, 0), 60);
        engine.SetCalibrationVolume(50);

        Assert.Equal(0, output.Calls);
    }

    [Fact]
    public void Volume_GoesToWindowsAtOnce_Clamped()
    {
        var (engine, output) = Calibrating();

        engine.SetCalibrationVolume(35);
        engine.SetCalibrationVolume(140);
        engine.SetCalibrationVolume(-5);

        Assert.Equal([35, 100, 0], output.Volumes.TakeLast(3));
    }

    [Fact]
    public void KeysAndMenuDoNothingWhileCalibrating()
    {
        var (engine, output) = Calibrating();
        var (calls, before) = (output.Calls, engine.Snapshot());

        engine.Handle(CabAction.TableUp, 0);
        engine.Handle(CabAction.FrontUp, 0);
        engine.Handle(CabAction.GlobalDown, 0);
        engine.Handle(CabAction.NightMode, 0);
        engine.ApplyMenu(new MenuEffect(MenuRow.Backbox, +1, OverlayKind.Popup), [], 0);
        engine.ApplyMenu(new MenuEffect(MenuRow.NightMode, +1, OverlayKind.Popup), [], 0);
        engine.Tick(10_000);

        Assert.Equal(calls, output.Calls);
        Assert.False(engine.NightMode);
        Assert.Equal(before.Tables, engine.Snapshot().Tables);
        Assert.Equal(before.GlobalVolume, engine.Snapshot().GlobalVolume);
    }

    [Fact]
    public void End_RestoresFileAndVolume()
    {
        var (engine, output) = Calibrating();
        engine.SetCalibrationTrial(new GroupLevels(-3, -5, 0, 0), 60);
        engine.SetCalibrationVolume(70);

        engine.EndCalibration();
        engine.Tick(10_000);

        Assert.False(engine.Calibrating);
        Assert.Equal(Normal("System").Include, output.Includes[^1]);
        Assert.Equal(40, output.Volumes[^1]); // System 80 x global 50 %
        Assert.Empty(output.Saves);
    }

    [Fact]
    public void TableStart_EndsCalibration()
    {
        var (engine, output) = Calibrating();
        engine.SetCalibrationVolume(70);

        engine.TableChanged(Table, 0);

        Assert.False(engine.Calibrating);
        Assert.Equal(Normal(Table).Include, output.Includes[^1]);
        Assert.Equal(20, output.Volumes[^1]); // the table's 40 x global 50 %
    }

    [Fact]
    public void PopperStart_KeepsCalibrating()
    {
        var (engine, output) = Calibrating();
        engine.SetCalibrationVolume(70);

        engine.TableChanged("PinUP Popper", 0);

        Assert.True(engine.Calibrating);
        Assert.Equal(70, output.Volumes[^1]);
        Assert.Equal(EqApoFile.BuildCalibration(Flat, null, Sound), output.Includes[^1]);
    }

    [Fact]
    public void Flush_WhileCalibrating_WritesTheNormalState()
    {
        var (engine, output) = Calibrating();
        engine.SetCalibrationTrial(new GroupLevels(-3, -5, 0, 0), 60);
        engine.SetCalibrationVolume(70);

        engine.Flush();

        Assert.False(engine.Calibrating);
        Assert.Equal(Normal("System").Include, output.Includes[^1]);
        Assert.Equal(40, output.Volumes[^1]);
    }

    [Fact]
    public void EndWithoutBegin_DoesNothing()
    {
        var (engine, output) = Engine();

        engine.EndCalibration();
        engine.Tick(10_000);

        Assert.Equal(0, output.Calls);
    }

    [Fact]
    public void BeginTwice_SecondReturnsTrueAndKeepsTheTrial()
    {
        var (engine, output) = Calibrating();
        engine.SetCalibrationTrial(new GroupLevels(-3, -5, 0, 0), 60);
        engine.SetCalibrationVolume(70);
        var calls = output.Calls;

        Assert.True(engine.BeginCalibration());

        Assert.Equal(calls, output.Calls);
        engine.TableChanged("PinUP Popper", 0);
        Assert.Equal(EqApoFile.BuildCalibration(new GroupLevels(-9, -11, -6, 0), 60, Sound), output.Includes[^1]);
    }

    [Fact]
    public void BeginAgainAfterEnd_StartsFlatAgain()
    {
        var (engine, output) = Calibrating();
        engine.SetCalibrationTrial(new GroupLevels(-3, -5, 0, 0), 60);
        engine.EndCalibration();

        Assert.True(engine.BeginCalibration());

        Assert.Equal(EqApoFile.BuildCalibration(Flat, null, Sound), output.Includes[^1]);
        Assert.Equal(10, output.Volumes[^1]);
    }

    [Fact]
    public void SetSound_WhileCalibrating_RewritesTheCheckFileImmediately()
    {
        var (engine, output) = Calibrating();
        var swapped = Sound with { JacksSwapped = true };
        var countBefore = output.Includes.Count;

        engine.SetSound(swapped, 0);

        // Written immediately without waiting for Tick / WriteScheduler
        Assert.Equal(countBefore + 1, output.Includes.Count);
        Assert.Equal(EqApoFile.BuildCalibration(Flat, null, swapped), output.Includes[^1]);
        Assert.Contains("Copy: SL=RL SR=RR RL=SL RR=SR", output.Includes[^1]);
    }

    [Fact]
    public void EndCalibration_AfterSetSound_NormalFileHasTheJackSwap()
    {
        var (engine, output) = Calibrating();
        engine.SetSound(Sound with { JacksSwapped = true }, 0);

        engine.EndCalibration();
        engine.Tick(10_000);

        Assert.Equal(Normal("System", Data() with { Sound = Sound with { JacksSwapped = true } }).Include, output.Includes[^1]);
        Assert.True(output.Saves[^1].Sound!.JacksSwapped);
    }

    [Fact]
    public void SaveSpeakerCheck_StoresSummaryAndDate()
    {
        var (engine, output) = Engine();
        Assert.Null(engine.State.SpeakerCheck);

        engine.SaveSpeakerCheck("all correct", Today, 0);

        Assert.Equal(new SpeakerCheckResult("all correct", "2026-11-12"), engine.State.SpeakerCheck);
        engine.Tick(2000);
        Assert.Equal(new SpeakerCheckResult("all correct", "2026-11-12"), output.Saves[^1].SpeakerCheck);
    }

    [Fact]
    public void SaveToolWindowPlace_IsInTheStateAndSaved()
    {
        var (engine, output) = Engine();
        Assert.Null(engine.State.ToolWindow);

        engine.SaveToolWindowPlace(new ToolWindowPlace(90, 2500, 300), 0);

        Assert.Equal(new ToolWindowPlace(90, 2500, 300), engine.State.ToolWindow);
        engine.Tick(2000);
        Assert.Equal(new ToolWindowPlace(90, 2500, 300), output.Saves[^1].ToolWindow);
    }

    [Fact]
    public void SaveToolWindowPlace_SamePlace_DoesNotSaveAgain()
    {
        var (engine, output) = Engine();
        engine.SaveToolWindowPlace(new ToolWindowPlace(90, 2500, 300), 0);
        engine.Tick(2000);
        var saves = output.Saves.Count;

        engine.SaveToolWindowPlace(new ToolWindowPlace(90, 2500, 300), 3000);
        engine.Tick(6000);

        Assert.Equal(saves, output.Saves.Count);
    }

    [Fact]
    public void State_Calibrating_IsTrueBetweenBeginAndEnd()
    {
        var (engine, _) = Engine();
        Assert.False(engine.State.Calibrating);

        engine.BeginCalibration();
        Assert.True(engine.State.Calibrating);

        engine.EndCalibration();
        Assert.False(engine.State.Calibrating);
    }
}
