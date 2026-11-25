using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public sealed class ControlsSettingsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cabsound-controls-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void PinVolPreset_ExpandsEitherCtrlCombosToBothCtrlKeys()
    {
        var controls = ControlsSettings.PinVol();
        var runtime = controls.ToRuntimeBindings();

        Assert.Equal(ControlPreset.PinVol, controls.Layout);
        Assert.False(controls.UsesJoystick());
        // 4 single keys + 8 Ctrl combos x 2 (Left + Right Ctrl) = 20 runtime bindings
        Assert.Equal(20, runtime.Count);
        Assert.Contains(runtime, b => b.Action == CabAction.OpenMenu && b.Combo && b.Inputs.SequenceEqual([InputId.Key(0xA2), InputId.Key(0xAF)]));
        Assert.Contains(runtime, b => b.Action == CabAction.OpenMenu && b.Combo && b.Inputs.SequenceEqual([InputId.Key(0xA3), InputId.Key(0xAF)]));
    }

    [Fact]
    public void MinimalPreset_HasTapVolumeAndHoldMenuAndNightMode()
    {
        var controls = ControlsSettings.Minimal();

        Assert.Equal(ControlPreset.Minimal, controls.Layout);
        Assert.Equal(4, controls.Bindings.Count);
        Assert.True(controls.Find(CabAction.OpenMenu) is { Hold: true, HoldMs: 1000 });
        Assert.True(controls.Find(CabAction.NightMode) is { Hold: true, HoldMs: 2000 });
        Assert.Null(controls.Find(CabAction.BackglassUp));
    }

    [Fact]
    public void WithBinding_AndClearAction_SwitchToCustomAndTrackJoystick()
    {
        var controls = ControlsSettings.PinVol()
            .WithBinding(ControlsSettings.FromCaptured(CabAction.TableUp, [InputId.Joy(20)], null))
            .ClearAction(CabAction.GlobalDown);

        Assert.Equal(ControlPreset.Custom, controls.Layout);
        Assert.True(controls.UsesJoystick());
        Assert.Equal([InputId.Joy(20)], controls.Find(CabAction.TableUp)!.Inputs);
        Assert.Null(controls.Find(CabAction.GlobalDown));
    }

    [Fact]
    public void FromCaptured_ComboWithCtrlSetsEitherCtrl_AndDisablesHold()
    {
        var existing = new ActionBinding(CabAction.OpenMenu, [InputId.Key(0xAF)], Hold: true, HoldMs: 1000);
        var updated = ControlsSettings.FromCaptured(CabAction.OpenMenu, [InputId.Key(InputNames.VkLeftCtrl), InputId.Key(0x73)], existing);

        Assert.True(updated.Combo);
        Assert.True(updated.EitherCtrl);
        Assert.False(updated.Hold);
        Assert.Equal("Ctrl + F4", InputNames.Format(updated));
    }

    [Fact]
    public void WithTrigger_TogglesHoldAndClearsCombo()
    {
        var controls = ControlsSettings.Minimal()
            .WithTrigger(CabAction.OpenMenu, hold: true, holdMs: 2000);

        Assert.Equal(ControlPreset.Custom, controls.Layout);
        Assert.Equal(2000, controls.Find(CabAction.OpenMenu)!.HoldMs);
    }

    [Fact]
    public void ResolveMenuKeys_FollowsAutoSetting()
    {
        var vpx = new MenuKeys(0xA0, 0xA1, 0xA2, 0xA3, 0x31, 0x50);
        var custom = new MenuKeys(0x41, 0x42, 0x43, 0x44, 0x45, 0x46);

        Assert.Equal(vpx, ControlsSettings.PinVol(autoMenuKeys: true, customMenuKeys: custom).ResolveMenuKeys(vpx));
        Assert.Equal(custom, ControlsSettings.PinVol(autoMenuKeys: false, customMenuKeys: custom).ResolveMenuKeys(vpx));
        Assert.Equal(ControlsSettings.DefaultMenuKeys, ControlsSettings.PinVol(autoMenuKeys: false).ResolveMenuKeys(null));
    }

    [Fact]
    public void ControlsSettings_RoundTripsInCabSoundJson()
    {
        var path = Path.Combine(_dir, CabSoundDataFile.FileName);
        var controls = ControlsSettings.Minimal(autoMenuKeys: false, customMenuKeys: ControlsSettings.DefaultMenuKeys)
            .WithBinding(new ActionBinding(CabAction.FrontUp, [InputId.Joy(17)]));

        CabSoundDataFile.Save(path, CabSoundData.Defaults() with { Controls = controls });
        var loaded = CabSoundDataFile.Load(path);

        Assert.Equal(ControlPreset.Custom, loaded.Controls!.Layout);
        Assert.False(loaded.Controls.AutoMenuKeys);
        Assert.Equal(ControlsSettings.DefaultMenuKeys, loaded.Controls.CustomMenuKeys);
        Assert.Equal([InputId.Joy(17)], loaded.Controls.Find(CabAction.FrontUp)!.Inputs);
    }

    [Fact]
    public void Engine_SetControls_SavesAndSupportsUndo()
    {
        var saves = new List<CabSoundData>();
        var output = new CaptureOutput(saves);
        var engine = new CabSoundEngine(CabSoundData.Defaults(), output);

        engine.SaveSettingsUndoPoint();
        engine.SetControls(ControlsSettings.Minimal(), 100);
        Assert.Equal(ControlPreset.Minimal, engine.State.ControlsOrDefault.Layout);

        engine.Tick(3000);
        Assert.Equal(ControlPreset.Minimal, saves[^1].Controls!.Layout);

        engine.Undo(4000);
        Assert.Equal(ControlPreset.PinVol, engine.State.ControlsOrDefault.Layout);
    }

    [Fact]
    public void ControlsOrDefault_IsStableAndValueEqualAcrossEngineUpdates()
    {
        var engine = new CabSoundEngine(CabSoundData.Defaults(), new CaptureOutput([]));
        var before = engine.State.ControlsOrDefault;

        engine.TableChanged("VP.Test", 10);
        engine.Handle(CabAction.TableUp, 20);
        var after = engine.State.ControlsOrDefault;

        Assert.Same(before, after);
        Assert.True(before.SameAs(after));
        Assert.True(ControlsSettings.PinVol().SameAs(ControlsSettings.PinVol()));
        Assert.False(ControlsSettings.PinVol().SameAs(ControlsSettings.Minimal()));
    }

    private sealed class CaptureOutput(List<CabSoundData> saves) : IEngineOutput
    {
        public void SetVolume(int percent) { }
        public void WriteInclude(string text) { }
        public void SaveData(CabSoundData data) => saves.Add(data);
        public void ShowPopup(PopupInfo info) { }
    }
}
