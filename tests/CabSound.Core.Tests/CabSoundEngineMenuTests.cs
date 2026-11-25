using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class CabSoundEngineMenuTests
{
    private sealed class FakeOutput : IEngineOutput
    {
        public List<int> Volumes { get; } = [];
        public List<PopupInfo> Popups { get; } = [];

        public void SetVolume(int percent) => Volumes.Add(percent);
        public void WriteInclude(string text) { }
        public void SaveData(CabSoundData data) { }
        public void ShowPopup(PopupInfo info) => Popups.Add(info);
    }

    private static readonly MonitorInfo[] NoMonitors = [];

    private static CabSoundData Data(int backglass = 0) => new(
        new GroupLevels(-10, 10, 10, 0), false, 10,
        [new TableLevel("System", 100, 66, 0, 0, 0), new TableLevel("VP.A", 43, 66, backglass, 0, -1)]);

    private static (CabSoundEngine Engine, FakeOutput Out) OnTableA(CabSoundData data)
    {
        var output = new FakeOutput();
        var engine = new CabSoundEngine(data, output);
        engine.TableChanged("VP.A", 0);
        output.Volumes.Clear();
        return (engine, output);
    }

    private static MenuEffect Effect(MenuRow row, int dir, OverlayKind window = OverlayKind.Popup) => new(row, dir, window);

    [Fact]
    public void ApplyMenu_Backbox_ChangesWithoutPopup()
    {
        var (engine, output) = OnTableA(Data());

        engine.ApplyMenu(Effect(MenuRow.Backbox, +1), NoMonitors, 10);

        Assert.Equal(1, engine.Current.BackglassDb);
        Assert.Empty(output.Popups);
    }

    [Fact]
    public void ApplyMenu_TableVolume_ChangesWithoutPopup()
    {
        var (engine, output) = OnTableA(Data());

        engine.ApplyMenu(Effect(MenuRow.TableVolume, +1), NoMonitors, 10);

        Assert.Equal(44, engine.Current.Volume);
        Assert.Equal([44], output.Volumes);
        Assert.Empty(output.Popups);
    }

    [Fact]
    public void ApplyMenu_RespectsBoostLimit()
    {
        var (engine, _) = OnTableA(Data(backglass: 6));

        engine.ApplyMenu(Effect(MenuRow.Backbox, +1), NoMonitors, 10);

        Assert.Equal(6, engine.Current.BackglassDb);
    }

    [Fact]
    public void ApplyMenu_FrontAndRear_ChangeWithoutPopup()
    {
        var (engine, output) = OnTableA(Data());

        engine.ApplyMenu(Effect(MenuRow.FrontExciters, +1), NoMonitors, 10);
        engine.ApplyMenu(Effect(MenuRow.RearExciters, -1), NoMonitors, 10);

        Assert.Equal(0, engine.Current.FrontDb);
        Assert.Equal(-1, engine.Current.RearDb);
        Assert.Empty(output.Popups);
    }

    [Fact]
    public void ApplyMenu_GlobalRow_ChangesGlobal()
    {
        var (engine, output) = OnTableA(Data() with { GlobalVolume = 80 });

        engine.ApplyMenu(Effect(MenuRow.GlobalVolume, -1), NoMonitors, 10);

        Assert.Equal(79, engine.Snapshot().GlobalVolume);
        Assert.Empty(output.Popups);
    }

    [Fact]
    public void ApplyMenu_GlobalRow_ChangesNightInNightMode()
    {
        var (engine, output) = OnTableA(Data() with { GlobalVolume = 80, NightVolume = 50 });
        engine.SetNightMode(true);

        engine.ApplyMenu(Effect(MenuRow.GlobalVolume, -1), NoMonitors, 10);

        Assert.Equal(49, engine.Snapshot().NightVolume);
        Assert.Equal(80, engine.Snapshot().GlobalVolume);
        Assert.Empty(output.Popups);
    }

    [Fact]
    public void ApplyMenu_NightMode_LeftOffRightOn()
    {
        var (engine, output) = OnTableA(Data());

        engine.ApplyMenu(Effect(MenuRow.NightMode, +1), NoMonitors, 10);
        Assert.True(engine.NightMode);

        engine.ApplyMenu(Effect(MenuRow.NightMode, -1), NoMonitors, 10);
        Assert.False(engine.NightMode);
        Assert.Empty(output.Popups);
    }

    [Fact]
    public void ApplyMenu_Reset()
    {
        var (engine, _) = OnTableA(Data(backglass: 2));

        engine.ApplyMenu(Effect(MenuRow.Reset, +1), NoMonitors, 10);

        Assert.Equal(new TableLevel("VP.A", 100, 66, 0, 0, 0), engine.Current);
    }

    [Fact]
    public void ApplyMenu_Rotation_ChangesMenuPlacement()
    {
        var (engine, _) = OnTableA(Data());

        engine.ApplyMenu(Effect(MenuRow.Rotation, +1, OverlayKind.Menu), NoMonitors, 10);

        Assert.Equal(90, engine.State.Overlays.Menu.Rotation);
        Assert.Equal(0, engine.State.Overlays.Popup.Rotation);
    }

    [Fact]
    public void ApplyMenu_ExciterLowCutRow_StepsThroughTheChoices_LeastToMostBass()
    {
        var (engine, _) = OnTableA(Data());

        engine.ApplyMenu(Effect(MenuRow.ExciterLowCut, -1), NoMonitors, 0);
        Assert.Null(engine.Current.ExciterLowCut); // already at Cabinet's cut

        engine.ApplyMenu(Effect(MenuRow.ExciterLowCut, +1), NoMonitors, 0);
        Assert.Equal(150, engine.Current.ExciterLowCut);

        engine.ApplyMenu(Effect(MenuRow.ExciterLowCut, +1), NoMonitors, 0);
        Assert.Equal(120, engine.Current.ExciterLowCut);
        Assert.Null(engine.Current.BackboxLowCut);
    }

    [Fact]
    public void ApplyMenu_BackboxLowCutRow_ChangesOnlyTheBackbox()
    {
        var (engine, output) = OnTableA(Data());

        engine.ApplyMenu(Effect(MenuRow.BackboxLowCut, +1), NoMonitors, 0);

        Assert.Equal(150, engine.Current.BackboxLowCut);
        Assert.Null(engine.Current.ExciterLowCut);
        Assert.Empty(output.Popups);
    }

    [Fact]
    public void ApplyMenu_RearExciterLowCutRow_ChangesOnlyRearExciters()
    {
        var (engine, output) = OnTableA(Data());

        engine.ApplyMenu(Effect(MenuRow.RearExciterLowCut, +1), NoMonitors, 0);
        engine.ApplyMenu(Effect(MenuRow.RearExciterLowCut, +1), NoMonitors, 0);

        Assert.Equal(120, engine.Current.RearExciterLowCut);
        Assert.Null(engine.Current.ExciterLowCut);
        Assert.Null(engine.Current.BackboxLowCut);
        Assert.Empty(output.Popups);
    }
}
