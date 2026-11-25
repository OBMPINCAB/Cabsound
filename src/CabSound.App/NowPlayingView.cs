using System;
using System.Windows;
using System.Windows.Controls;
using CabSound.Core;

namespace CabSound.App;

/// <summary>Now Playing tab: the running table's levels, night mode and global/night levels.</summary>
public sealed class NowPlayingView : StackPanel
{
    private readonly Workers _workers;
    private readonly TextBlock _name = new() { FontSize = 26, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _source = new() { Opacity = 0.7, Margin = new Thickness(0, 0, 0, 12) };
    private readonly LevelSlider _volume = new("Table volume", decibels: false);
    private readonly LevelSlider _backbox = new("Backbox", decibels: true);
    private readonly LevelSlider _front = new("Front exciters", decibels: true);
    private readonly LevelSlider _rear = new("Rear exciters", decibels: true);
    private readonly LowCutPicker _backboxCut = new("Backbox low cut", LowCutGroup.Backbox);
    private readonly LowCutPicker _exciterCut = new("Front exciter low cut", LowCutGroup.Exciters);
    private readonly LowCutPicker _rearExciterCut = new("Rear exciter low cut", LowCutGroup.RearExciters);
    private readonly CheckBox _night = new() { Content = "Night mode (120 Hz low cut + −3 dB exciters)", Margin = new Thickness(0, 16, 0, 4) };
    private readonly LevelSlider _global = new("Global volume", decibels: false);
    private readonly LevelSlider _nightVolume = new("Night volume", decibels: false);
    private readonly Button _undo = new() { Content = "Undo", Margin = new Thickness(8, 0, 0, 0) };
    private EngineState? _state;
    private bool _showing;

    public NowPlayingView(Workers workers)
    {
        _workers = workers;
        Margin = new Thickness(16);
        var reset = new Button { Content = "Reset this table" };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(reset);
        buttons.Children.Add(_undo);
        foreach (UIElement e in new UIElement[] { _name, _source, _volume, _backbox, _front, _rear, _backboxCut, _exciterCut, _rearExciterCut, _night, _global, _nightVolume, buttons })
            Children.Add(e);

        Edit(_volume, LevelField.Volume);
        Edit(_backbox, LevelField.Backglass);
        Edit(_front, LevelField.Front);
        Edit(_rear, LevelField.Rear);
        _backboxCut.Changed += v => { if (_state is { } s) LowCutPicker.Apply(_workers, s, s.Table, LowCutGroup.Backbox, v); };
        _exciterCut.Changed += v => { if (_state is { } s) LowCutPicker.Apply(_workers, s, s.Table, LowCutGroup.Exciters, v); };
        _rearExciterCut.Changed += v => { if (_state is { } s) LowCutPicker.Apply(_workers, s, s.Table, LowCutGroup.RearExciters, v); };
        Setting(_global, (e, v, now) => e.SetGlobal(v, now));
        Setting(_nightVolume, (e, v, now) => e.SetNight(v, now));
        _night.Click += (_, _) => { if (!_showing) { var on = _night.IsChecked == true; _workers.Run((e, _) => e.SetNightMode(on)); } };
        reset.Click += (_, _) =>
        {
            if (_state is not { } s) return;
            _workers.Run((e, now) => { e.SaveUndoPoint([s.Table]); e.Reset(s.Table, now); });
        };
        _undo.Click += (_, _) => _workers.Run((e, now) => e.Undo(now));
    }

    /// <summary>Slider edits change one value of the table shown, as it is on the engine thread.</summary>
    private void Edit(LevelSlider slider, LevelField field)
    {
        slider.GestureStarted += () => { if (_state is { } s) _workers.Run((e, _) => e.SaveUndoPoint([s.Table])); };
        slider.Changed += v => { if (_state is { } s) _workers.Run((e, now) => e.SetTableValue(s.Table, field, v, now)); };
    }

    private void Setting(LevelSlider slider, Action<CabSoundEngine, int, long> apply)
    {
        slider.GestureStarted += () => _workers.Run((e, _) => e.SaveSettingsUndoPoint());
        slider.Changed += v => _workers.Run((e, now) => apply(e, v, now));
    }

    public void ShowState(EngineState s)
    {
        _state = s;
        _showing = true;
        _name.Text = TableSearch.DisplayName(s.Table);
        _source.Text = TableSearch.Source(s.Table);
        var active = s.NightMode ? s.NightVolume : s.GlobalVolume;
        _volume.Show(s.Levels.Volume, 0, 100, $"Windows {Math.Round(s.Levels.Volume * active / 100.0, MidpointRounding.AwayFromZero)}");
        _backbox.Show(s.Levels.BackglassDb, -s.CutLimitDb, s.BoostLimitDb, $"written {s.Applied.Backglass} dB", s.AtLimit(s.Levels, SsfGroup.Backglass));
        _front.Show(s.Levels.FrontDb, -s.CutLimitDb, s.BoostLimitDb, $"written {s.Applied.Front} dB", s.AtLimit(s.Levels, SsfGroup.Front));
        _rear.Show(s.Levels.RearDb, -s.CutLimitDb, s.BoostLimitDb, $"written {s.Applied.Rear} dB", s.AtLimit(s.Levels, SsfGroup.Rear));
        _backboxCut.Show(s.Levels.BackboxLowCut, s.Sound);
        _exciterCut.Show(s.Levels.ExciterLowCut, s.Sound);
        _rearExciterCut.Show(s.Levels.RearExciterLowCut, s.Sound);
        _night.IsChecked = s.NightMode;
        _global.Show(s.GlobalVolume, 0, 100, s.NightMode ? "" : "active");
        _nightVolume.Show(s.NightVolume, 0, 100, s.NightMode ? "active" : "");
        _undo.IsEnabled = s.CanUndo;
        _showing = false;
    }
}
