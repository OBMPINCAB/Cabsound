using System;
using System.Windows;
using System.Windows.Controls;
using CabSound.Core;

namespace CabSound.App;

/// <summary>
/// Rotatable cabinet tool window for dialing in the CSD SSF amp's Subwoofer (70 Hz kick on L R) and
/// Bass Shaker (42 Hz thump on RL RR + SL SR) with pulsed tactile test bursts when no table is running.
/// </summary>
public sealed class BassCheckWindow : Window
{
    private readonly CalibrationSession _session;
    private readonly TextBlock _status = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) };
    private readonly TextBlock _tip = new() { TextWrapping = TextWrapping.Wrap, Opacity = 0.85, Margin = new Thickness(0, 0, 0, 12) };
    private bool _stopped;

    public BassCheckWindow(CalibrationSession session, EngineState state, Action<ToolWindowPlace> savePlace)
    {
        _session = session;
        Title = "CabSound: test sub & shaker";

        var intro = new TextBlock
        {
            Text = "Plays clean pulsed thumps so you can dial your CSD amp's Sub and Shaker Volume + Frequency knobs " +
                   "without starting a table. Start with the Volume slider low, then bring it up.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
        };

        var subBtn = new Button { Content = "Play Sub kick (70 Hz)", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        var shakerBtn = new Button { Content = "Play Shaker thump (42 Hz)", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        var stopBtn = new Button { Content = "Stop", Padding = new Thickness(12, 6, 12, 6) };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        row.Children.Add(subBtn);
        row.Children.Add(shakerBtn);
        row.Children.Add(stopBtn);

        var close = new Button { Content = "Close", Width = 90, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };

        var panel = new StackPanel { Margin = new Thickness(16) };
        foreach (var e in new UIElement[] { intro, _status, _tip, row, new VolumeRow(session), close }) panel.Children.Add(e);
        ToolWindowFrame.Attach(this, panel, state.ToolWindow, savePlace);

        subBtn.Click += (_, _) => PlaySub(state.Calibration);
        shakerBtn.Click += (_, _) => PlayShaker(state.Calibration);
        stopBtn.Click += (_, _) => StopTone();
        close.Click += (_, _) => Close();
        _session.Stopped += OnStopped;

        StopTone();
    }

    protected override void OnClosed(EventArgs e)
    {
        _session.Stopped -= OnStopped;
        if (!_stopped) _session.End();
        base.OnClosed(e);
    }

    private void PlaySub(GroupLevels cal)
    {
        _status.Text = "Now playing: Sub kick (70 Hz on Backbox / Sub)";
        _tip.Text = "Turn Sub Freq down to ~85 Hz (9–10 o'clock) so kicks sound tight rather than boomy, " +
                    "then set Sub Volume for crisp chest punch.";
        _session.PlaySamples(
            new GroupLevels(cal.Backglass, -24, -24, 0),
            (rate, ch) => TestTone.Pulse(rate, ch, [0, 1], -12, hz: 70, pulseSeconds: 0.18, periodSeconds: 1.0).Read,
            exciterCut: 90,
            backboxCut: 40);
    }

    private void PlayShaker(GroupLevels cal)
    {
        _status.Text = "Now playing: Shaker thump (42 Hz on Front + Rear exciters)";
        _tip.Text = "Set Shaker Freq around ~65 Hz (9–10 o'clock) and raise Shaker Volume until each pulse " +
                    "gives a clean physical thump through the cabinet without continuous buzz.";
        _session.PlaySamples(
            new GroupLevels(-24, cal.Rear, cal.Front, 0),
            (rate, ch) => TestTone.Pulse(rate, ch, [4, 5, 6, 7], -12, hz: 42, pulseSeconds: 0.25, periodSeconds: 1.1).Read,
            exciterCut: 40,
            backboxCut: 90);
    }

    private void StopTone()
    {
        _status.Text = "Stopped";
        _tip.Text = "Pick Sub kick or Shaker thump below.";
        _session.PlaySamples(new GroupLevels(0, 0, 0, 0), null);
    }

    private void OnStopped(string reason)
    {
        var owner = Owner;
        _stopped = true;
        Close();
        if (owner is { IsVisible: true }) MessageBox.Show(owner, reason, "CabSound", MessageBoxButton.OK, MessageBoxImage.Warning);
        else MessageBox.Show(reason, "CabSound", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
