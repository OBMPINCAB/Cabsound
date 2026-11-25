using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CabSound.Core;
using CabSound.Platform;

namespace CabSound.App;

/// <summary>
/// Sound tab: cabinet-wide low cuts, 250 Hz dip, jack swap, manual cabinet balance (calibration),
/// speaker check, and the sound card output the table volume is set on.
/// </summary>
public sealed class SoundView : StackPanel
{
    private readonly Workers _workers;
    private readonly ComboBox _backboxCut = new() { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox _exciterCut = new() { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox _rearExciterCut = new() { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox _shakerBoost = new() { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly CheckBox _bassPunch = new() { Content = "Punchy bass EQ (+2.5 dB kick punch at 72 Hz, −3.5 dB mud cut at 185 Hz on backbox/sub)", Margin = new Thickness(0, 10, 0, 0) };
    private readonly CheckBox _dip = new() { Content = "Exciter clarity EQ (−3.5 dB mud cut at 250 Hz, +2.5 dB clack boost at 2.8 kHz; backbox clean)", Margin = new Thickness(0, 8, 0, 0) };
    private readonly CheckBox _swap = new() { Content = "Side and Rear jacks are swapped by the Windows driver (swap them back)", Margin = new Thickness(0, 8, 0, 0) };
    private readonly LevelSlider _calBackbox = new("Backbox", decibels: true);
    private readonly LevelSlider _calFront = new("Front exciters", decibels: true);
    private readonly LevelSlider _calRear = new("Rear exciters", decibels: true);
    private readonly TextBlock _headroom = new() { Opacity = 0.7, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _zeroBalance = new() { Content = "Reset balance to 0 dB", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button _undo = new() { Content = "Undo", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 0, 0, 0) };
    private readonly ComboBox _card = new() { MinWidth = 360, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _cardNote = new() { TextWrapping = TextWrapping.Wrap, Opacity = 0.7, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _checked = new() { Opacity = 0.7, Margin = new Thickness(0, 4, 0, 0) };
    private CalibrationSession? _session;   // the open speaker check
    private List<(string Id, string Name)> _outputs = [];
    private EngineState? _state;
    private bool _showing;

    public SoundView(Workers workers)
    {
        _workers = workers;
        Margin = new Thickness(16);

        Children.Add(new TextBlock
        {
            Text = "Cabinet-wide sound settings. Individual tables can override the low cuts on the Now Playing and Tables tabs.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
        });

        var cutLabels = LowCutChoices.CabinetChoices.Select(LowCutChoices.CabinetLabel).ToList();
        _backboxCut.ItemsSource = cutLabels;
        _exciterCut.ItemsSource = cutLabels;
        _rearExciterCut.ItemsSource = cutLabels;
        _shakerBoost.ItemsSource = new[] { "Off", "+3 dB (recommended)", "+6 dB (strong)" };
        Children.Add(CutRow("Backbox low cut (sub)", _backboxCut));
        Children.Add(CutRow("Front exciter low cut (shaker)", _exciterCut));
        Children.Add(CutRow("Rear exciter low cut (shaker)", _rearExciterCut));
        Children.Add(CutRow("Shaker 42 Hz boost", _shakerBoost));
        Children.Add(_bassPunch);
        Children.Add(_dip);
        Children.Add(_swap);

        Children.Add(new TextBlock { Text = "Cabinet balance", FontSize = 16, Margin = new Thickness(0, 20, 0, 4) });
        Children.Add(new TextBlock
        {
            Text = "Base dB per speaker group (applied to every table). Turn the backbox knob down on the amp so all three sliders can sit at 0 dB (recovers 20 dB of signal for the backbox and sub).",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.7, Margin = new Thickness(0, 0, 0, 6),
        });
        Children.Add(_calBackbox);
        Children.Add(_calFront);
        Children.Add(_calRear);
        Children.Add(_headroom);
        var balanceButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        balanceButtons.Children.Add(_zeroBalance);
        balanceButtons.Children.Add(_undo);
        Children.Add(balanceButtons);

        Children.Add(new TextBlock { Text = "Speakers & Tuning Tools", FontSize = 16, Margin = new Thickness(0, 20, 0, 6) });
        var toolButtons = new StackPanel { Orientation = Orientation.Horizontal };
        var check = new Button { Content = "Check the speakers…", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        var bassCheck = new Button { Content = "Test Sub & Shaker…", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        var guideBtn = new Button { Content = "Tuning guide…", Padding = new Thickness(12, 4, 12, 4) };
        check.Click += (_, _) => OpenSpeakerCheck();
        bassCheck.Click += (_, _) => OpenBassCheck();
        guideBtn.Click += (_, _) => OpenGuide();
        toolButtons.Children.Add(check);
        toolButtons.Children.Add(bassCheck);
        toolButtons.Children.Add(guideBtn);
        Children.Add(toolButtons);
        Children.Add(_checked);
        Children.Add(new TextBlock
        {
            Text = "Check each speaker's position, play 70 Hz Sub / 42 Hz Shaker pulses while adjusting the amp knobs, " +
                   "or open the tuning checklist (also available in-game via Magna + Vol Up → Guide ›).",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.7, Margin = new Thickness(0, 4, 0, 0),
        });

        Children.Add(new TextBlock { Text = "Sound card", FontSize = 16, Margin = new Thickness(0, 20, 0, 6) });
        Children.Add(_card);
        Children.Add(_cardNote);
        _card.DropDownOpened += (_, _) => ShowCards();   // outputs come and go (USB, HDMI)
        _card.SelectionChanged += (_, _) => PickCard();

        _backboxCut.SelectionChanged += (_, _) => PickCabinetCut(LowCutGroup.Backbox, _backboxCut);
        _exciterCut.SelectionChanged += (_, _) => PickCabinetCut(LowCutGroup.Exciters, _exciterCut);
        _rearExciterCut.SelectionChanged += (_, _) => PickCabinetCut(LowCutGroup.RearExciters, _rearExciterCut);
        _shakerBoost.SelectionChanged += (_, _) =>
        {
            if (_showing || _shakerBoost.SelectedIndex < 0) return;
            var boost = EqApoFile.ShakerBoostChoices[_shakerBoost.SelectedIndex];
            Change(s => s with { ShakerBoostDb = boost });
        };
        _bassPunch.Click += (_, _) => { var on = _bassPunch.IsChecked == true; Change(s => s with { BassPunch = on }); };
        _dip.Click += (_, _) => { var on = _dip.IsChecked == true; Change(s => s with { Dip250 = on }); };
        _swap.Click += (_, _) => { var on = _swap.IsChecked == true; Change(s => s with { JacksSwapped = on }); };
        EditCalibration(_calBackbox, SsfGroup.Backglass);
        EditCalibration(_calFront, SsfGroup.Front);
        EditCalibration(_calRear, SsfGroup.Rear);
        _zeroBalance.Click += (_, _) => _workers.Run((e, now) => { e.SaveSettingsUndoPoint(); e.ResetCalibration(now); });
        _undo.Click += (_, _) => _workers.Run((e, now) => e.Undo(now));
    }

    private static DockPanel CutRow(string label, ComboBox box)
    {
        var row = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        row.Children.Add(new TextBlock { Text = label, Width = 190, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(box);
        return row;
    }

    private void EditCalibration(LevelSlider slider, SsfGroup group)
    {
        slider.GestureStarted += () => _workers.Run((e, _) => e.SaveSettingsUndoPoint());
        slider.Changed += v => _workers.Run((e, now) => e.SetCalibrationGroup(group, v, now));
    }

    private void PickCabinetCut(LowCutGroup group, ComboBox box)
    {
        if (_showing || _state is not { } s || box.SelectedIndex < 0) return;
        var cut = LowCutChoices.CabinetChoices[box.SelectedIndex];
        if (LowCutChoices.IsRisky(group, cut)) LowCutPicker.WarnOnce(_workers, s);
        Change(sound => sound.WithCabinetCut(group, cut));
    }

    /// <summary>One setting changed in the window, applied to the settings as they are on the engine thread.</summary>
    private void Change(Func<SoundSettings, SoundSettings> change)
    {
        if (_showing || _state is null) return;
        _workers.Run((e, now) => { e.SaveSettingsUndoPoint(); e.SetSound(change(e.State.Sound), now); });
    }

    public void ShowState(EngineState s)
    {
        _state = s;
        _session?.OnState(s);
        _checked.Text = s.SpeakerCheck is { } result
            ? $"Speakers checked on {result.CheckedOn}: {result.Summary}"
            : "Speakers not checked yet";
        _showing = true;
        var choices = LowCutChoices.CabinetChoices.ToList();
        _backboxCut.SelectedIndex = choices.IndexOf(LowCutChoices.Cabinet(s.Sound, LowCutGroup.Backbox));
        _exciterCut.SelectedIndex = choices.IndexOf(LowCutChoices.Cabinet(s.Sound, LowCutGroup.Exciters));
        _rearExciterCut.SelectedIndex = choices.IndexOf(LowCutChoices.Cabinet(s.Sound, LowCutGroup.RearExciters));
        var boostIdx = ((IList<int>)EqApoFile.ShakerBoostChoices).IndexOf(s.Sound.ShakerBoostDb);
        _shakerBoost.SelectedIndex = boostIdx >= 0 ? boostIdx : 0;
        _bassPunch.IsChecked = s.Sound.BassPunch;
        _dip.IsChecked = s.Sound.Dip250;
        _swap.IsChecked = s.Sound.JacksSwapped;
        _calBackbox.Show(s.Calibration.Backglass, -30, 10, "");
        _calFront.Show(s.Calibration.Front, -30, 10, "");
        _calRear.Show(s.Calibration.Rear, -30, 10, "");
        _headroom.Text = $"Clipping headroom: −{s.HeadroomDb} dB (allows up to +{s.BoostLimitDb} dB per-table boost without clipping)";
        _zeroBalance.IsEnabled = s.Calibration != new GroupLevels(0, 0, 0, 0);
        _undo.IsEnabled = s.CanUndo;
        _showing = false;
        // Not on every state change (each key press sends one): only the first time and when the card changed
        if (_outputs.Count == 0 || _outputs.FindIndex(o => o.Id == s.SoundCardId) != _card.SelectedIndex) ShowCards();
    }

    /// <summary>Lists Windows' outputs with the saved card selected (nothing selected = the saved card is gone).</summary>
    private void ShowCards()
    {
        _showing = true;
        try { _outputs = AudioDevice.Outputs(); }
        catch (Exception e) { Log.Write($"Listing sound cards failed: {e.Message}"); _outputs = []; }
        _card.Items.Clear();
        foreach (var (_, name) in _outputs) _card.Items.Add(name);
        _card.SelectedIndex = _outputs.FindIndex(o => o.Id == _state?.SoundCardId);
        _cardNote.Text = _card.SelectedIndex < 0
            ? "The saved sound card isn't connected: table volume won't change. Pick the output the cabinet's amp is on."
            : "Table volume is set on this output. After changing it, press Check the speakers… above.";
        _showing = false;
    }

    private void OpenSpeakerCheck()
    {
        if (_state is not { } s) return;
        var session = new CalibrationSession(_workers, s);
        if ((CalibrationSession.CannotStart(s) ?? session.Start()) is { } why)
        {
            session.Dispose();
            MessageBox.Show(Window.GetWindow(this), why, "CabSound", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _session = session;
        _swap.IsEnabled = false;   // the check tracks the jack swap itself while it is open
        var window = new SpeakerCheckWindow(session, s, p => _workers.Run((e, now) => e.SaveToolWindowPlace(p, now))) { Owner = Window.GetWindow(this) };
        window.Closed += (_, _) =>
        {
            session.Dispose();
            if (_session == session) _session = null;
            _swap.IsEnabled = true;
        };
        window.Show();
    }

    private void OpenBassCheck()
    {
        if (_state is not { } s) return;
        var session = new CalibrationSession(_workers, s);
        if ((CalibrationSession.CannotStart(s) ?? session.Start()) is { } why)
        {
            session.Dispose();
            MessageBox.Show(Window.GetWindow(this), why, "CabSound", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _session = session;
        var window = new BassCheckWindow(session, s, p => _workers.Run((e, now) => e.SaveToolWindowPlace(p, now))) { Owner = Window.GetWindow(this) };
        window.Closed += (_, _) =>
        {
            session.Dispose();
            if (_session == session) _session = null;
        };
        window.Show();
    }

    private void OpenGuide()
    {
        var window = new GuideWindow(_state?.ToolWindow, p => _workers.Run((e, now) => e.SaveToolWindowPlace(p, now))) { Owner = Window.GetWindow(this) };
        window.Show();
    }

    private void PickCard()
    {
        if (_showing || _card.SelectedIndex < 0) return;
        var (id, name) = _outputs[_card.SelectedIndex];
        _workers.Run((e, now) => e.SetSoundCard(id, SoundCardChoice.NameHint(name), now));
    }
}
