using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CabSound.Core;
using CabSound.Platform;

namespace CabSound.App;

/// <summary>
/// "Check the speakers": a thin shell over <see cref="SpeakerCheck"/>. The test sound plays on one named
/// speaker at a time until the owner presses Next; he marks it right place, wrong place (and where) or not
/// heard. The result explains what is crossed and can switch the jack swap when front and rear are
/// exchanged (then the check runs again). A finished run is logged and its verdict saved; closing saves
/// nothing else.
/// </summary>
public sealed class SpeakerCheckWindow : Window
{
    private readonly CalibrationSession _session;
    private readonly SpeakerCheck _check;
    private readonly TextBlock _title = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) };
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
    private readonly TextBlock _verdict = new() { FontSize = 16, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _lines = new() { Opacity = 0.8, Margin = new Thickness(0, 0, 0, 12) };
    private readonly TextBlock _now = new() { FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) };
    private readonly StackPanel _marks = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
    private readonly RadioButton _right = Mark("Right place");
    private readonly RadioButton _wrong = Mark("Wrong place");
    private readonly RadioButton _notHeard = Mark("Not heard");
    private readonly TextBlock _where = new() { Text = "Where did you hear it?", Margin = new Thickness(0, 0, 0, 8) };
    private readonly Grid _places = new() { Margin = new Thickness(0, 0, 0, 12) };
    private readonly Dictionary<Speaker, Button> _placeButtons = [];
    private readonly StackPanel _listenRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _back = new() { Content = "Back", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _next = new() { Content = "Next", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _fix = new() { Content = "Switch \"Side and Rear jacks swapped\"", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 0, 12, 0) };
    private readonly Button _switchBack = new() { Content = "Switch it back", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 0, 12, 0) };
    private readonly Button _again = new() { Content = "Check again", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 0, 12, 0) };
    private readonly Button _close = new() { Width = 90 };   // not IsCancel: a stray Esc must not end the check

    private bool _swapped;          // the jack swap setting as it is now
    private bool _switchedHere;     // this window switched it (so it can offer to switch it back)
    private bool _reported;         // this run's result is logged and saved
    private bool _stopped;          // the session ended on its own (a table started, playback failed)
    private string _playing = "";   // what sounds now: step + speaker, so a refresh doesn't restart it
    private float[]? _word;
    private bool _wordTried;

    public SpeakerCheckWindow(CalibrationSession session, EngineState state, Action<ToolWindowPlace> savePlace)
    {
        _session = session;
        _check = new SpeakerCheck(state.Calibration);
        _swapped = state.Sound.JacksSwapped;
        Title = "CabSound: check the speakers";

        _right.Click += (_, _) => { _check.MarkRight(); Refresh(); };
        _wrong.Click += (_, _) => { _check.MarkWrong(); Refresh(); };
        _notHeard.Click += (_, _) => { _check.MarkNotHeard(); Refresh(); };
        foreach (var mark in new[] { _right, _wrong, _notHeard }) _marks.Children.Add(mark);

        BuildPlaces();
        var playAgain = new Button { Content = "Play again", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 0, 12, 0) };
        playAgain.Click += (_, _) => { _check.Play(); Play(force: true); Refresh(); };   // Refresh clears "(stopped)"; same key, no second start
        var stop = new Button { Content = "Stop", Padding = new Thickness(12, 0, 12, 0) };
        stop.Click += (_, _) => { _check.Stop(); Refresh(); };
        foreach (var e in new UIElement[] { playAgain, stop, _count }) _listenRow.Children.Add(e);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        foreach (var b in new[] { _back, _next, _fix, _switchBack, _again, _close }) buttons.Children.Add(b);

        var panel = new StackPanel { Margin = new Thickness(16) };
        foreach (var e in new UIElement[] { _title, _text, _verdict, _lines, _now, _marks, _where, _places, _listenRow, new VolumeRow(session), buttons }) panel.Children.Add(e);
        ToolWindowFrame.Attach(this, panel, state.ToolWindow, savePlace);

        _back.Click += (_, _) => { _check.Back(); Refresh(); };
        _next.Click += (_, _) => { _check.Next(); Refresh(); };
        _fix.Click += (_, _) => SwitchJackSwap(runAgain: true);
        _switchBack.Click += (_, _) => SwitchJackSwap(runAgain: false);
        _again.Click += (_, _) => RunAgain();
        _close.Click += (_, _) => Close();
        _session.Stopped += OnStopped;
        Loaded += (_, _) => Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        _session.Stopped -= OnStopped;
        if (!_stopped) _session.End();   // table levels and volume come back; a finished run is already saved
        base.OnClosed(e);
    }

    private static RadioButton Mark(string text) => new() { Content = text, GroupName = "mark", Margin = new Thickness(0, 0, 24, 0) };

    /// <summary>The cabinet from above: backbox at the top, the player at the bottom. Shown for "Wrong place".</summary>
    private void BuildPlaces()
    {
        foreach (var width in new[] { 150, 150 }) _places.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        _places.ColumnDefinitions.Add(new ColumnDefinition());
        var rows = new (Speaker Left, Speaker Right)[]
        {
            (Speaker.BackboxLeft, Speaker.BackboxRight),
            (Speaker.RearLeft, Speaker.RearRight),
            (Speaker.FrontLeft, Speaker.FrontRight),
        };
        for (var r = 0; r < rows.Length; r++)
        {
            _places.RowDefinitions.Add(new RowDefinition());
            Place(rows[r].Left, r, 0);
            Place(rows[r].Right, r, 1);
            var note = new TextBlock { Text = Note(rows[r].Left), Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetRow(note, r);
            Grid.SetColumn(note, 2);
            _places.Children.Add(note);
        }
    }

    private void Place(Speaker speaker, int row, int column)
    {
        var button = new Button { Content = Speakers.Name(speaker), Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(0, 10, 0, 10), HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => { _check.HeardAt(speaker); Refresh(); };
        Grid.SetRow(button, row);
        Grid.SetColumn(button, column);
        _places.Children.Add(button);
        _placeButtons[speaker] = button;
    }

    /// <summary>Which end of the playfield an exciter is at; empty for the backbox.</summary>
    private static string Note(Speaker speaker) => speaker switch
    {
        Speaker.FrontLeft or Speaker.FrontRight => "(near you)",
        Speaker.RearLeft or Speaker.RearRight => "(near the backbox)",
        _ => "",
    };

    private void RunAgain()
    {
        _check.Again();
        _reported = false;
        Refresh();
    }

    /// <summary>The fix (then a new run to confirm it) or its way back (the result stays).</summary>
    private void SwitchJackSwap(bool runAgain)
    {
        _swapped = !_swapped;
        _switchedHere = runAgain;
        _session.SetJackSwap(_swapped);
        Log.Write($"Speaker check: jack swap switched {(runAgain ? "" : "back ")}{(_swapped ? "on" : "off")}");
        if (runAgain) RunAgain();
        else Refresh();
    }

    private void OnStopped(string reason)
    {
        var owner = Owner;
        _stopped = true;
        Close();
        if (owner is { IsVisible: true }) MessageBox.Show(owner, reason, "CabSound", MessageBoxButton.OK, MessageBoxImage.Warning);
        else MessageBox.Show(reason, "CabSound", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Refresh()
    {
        var (prepare, listen, result) = (_check.Step == SpeakerStep.Prepare, _check.Step == SpeakerStep.Listen, _check.Step == SpeakerStep.Result);
        var verdict = _check.Verdict;
        if (verdict is not null && !_reported) Report(verdict);

        _title.Text = prepare ? "Step 1 of 3: Prepare" : listen ? "Step 2 of 3: Listen to each speaker" : "Step 3 of 3: Result";
        _text.Text = prepare
            ? "Stand at the playing position. Popper may stay open, but keep it quiet.\n\n"
              + "The test sound (three clacks and a word) now plays on both backbox speakers. "
              + "Set the volume below so it is clear, then press Next."
            : listen
                ? "This speaker plays until you press Next. Mark what you hear. "
                  + "Judge the place by the clacks; the word can be heard all over the cabinet."
                : verdict!.Advice + (_switchedHere && verdict.Kind == VerdictKind.AllCorrect
                    ? "\n\nThe jack swap setting was switched and stays that way." : "");
        _verdict.Text = verdict is null ? "" : char.ToUpperInvariant(verdict.Summary[0]) + verdict.Summary[1..] + ".";
        _lines.Text = verdict is null ? "" : string.Join("\n", verdict.Lines);

        if (listen)
        {
            _now.Text = $"Now playing: {Speakers.Name(_check.Current)} {Note(_check.Current)}".TrimEnd()
                        + (_check.Stopped ? "  (stopped)" : "");
            _count.Text = $"Speaker {_check.Number} of {Speakers.All.Count}";
            _right.IsChecked = _check.Mark == SpeakerMark.Right;
            _wrong.IsChecked = _check.Mark == SpeakerMark.Wrong;
            _notHeard.IsChecked = _check.Mark == SpeakerMark.NotHeard;
            foreach (var (speaker, button) in _placeButtons)
            {
                button.IsEnabled = speaker != _check.Current;
                button.FontWeight = speaker == _check.WrongPlace ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        Show(_verdict, result);
        Show(_lines, result);
        Show(_now, listen);
        Show(_marks, listen);
        Show(_where, listen && _check.Mark == SpeakerMark.Wrong);
        Show(_places, listen && _check.Mark == SpeakerMark.Wrong);
        Show(_listenRow, listen);
        Show(_back, listen);
        Show(_next, prepare || listen);
        _next.IsEnabled = _check.CanGoNext;
        Show(_fix, result && !_switchedHere && verdict!.Kind == VerdictKind.FrontRearExchanged);
        Show(_switchBack, result && _switchedHere && verdict!.Kind != VerdictKind.AllCorrect);
        Show(_again, result);
        _close.Content = result ? "Close" : "Cancel";

        Play(force: false);
    }

    private static void Show(UIElement element, bool visible) => element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>A finished run: one log line and the saved verdict.</summary>
    private void Report(SpeakerVerdict verdict)
    {
        _reported = true;
        Log.Write($"Speaker check: {verdict.Summary}; {string.Join(" | ", verdict.Lines)}; jack swap {(_swapped ? "on" : "off")}");
        _session.SaveSpeakerCheck(verdict.Summary);
    }

    /// <summary>
    /// Starts the sound for the step (silence on the result and after Stop). The same step again only
    /// restarts it when forced.
    /// </summary>
    private void Play(bool force)
    {
        var channels = _check.PlayingOn;
        var key = channels is null ? "" : $"{_check.Step} {string.Join("+", channels)} {_check.Number}";
        if (key == _playing && !force) return;
        _playing = key;
        _session.PlaySamples(_check.Trial, channels is null ? null
            : (rate, count) => new SpeakerCheckSound(rate, count, channels, Word(rate)).Read);
    }

    /// <summary>The spoken word, made once at the card's sample rate; null = Windows has no voice (clacks only).</summary>
    private float[]? Word(int sampleRate)
    {
        if (_wordTried) return _word;
        _wordTried = true;
        _word = SpokenWord.Render("here", sampleRate);
        if (_word is null) Log.Write("Speaker check: no Windows voice, clacks only");
        return _word;
    }
}

/// <summary>The speaker check window's volume slider: Windows volume while the window is open, starts at 10.</summary>
public sealed class VolumeRow : DockPanel
{
    private readonly Slider _slider = new() { Minimum = 0, Maximum = 100, Value = 10, SmallChange = 1, LargeChange = 5, IsSnapToTickEnabled = true, TickFrequency = 1 };
    private readonly TextBlock _text = new() { Width = 40, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

    public int Value => (int)_slider.Value;

    public VolumeRow(CalibrationSession session)
    {
        var label = new TextBlock { Text = "Volume", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        SetDock(label, Dock.Left);
        SetDock(_text, Dock.Right);
        Children.Add(label);
        Children.Add(_text);
        Children.Add(_slider);
        _text.Text = $"{Value}";
        _slider.ValueChanged += (_, _) =>
        {
            _text.Text = $"{Value}";
            session.SetVolume(Value);
        };
    }
}
