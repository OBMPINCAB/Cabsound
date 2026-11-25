using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CabSound.Core;
using CabSound.Platform;

namespace CabSound.App;

/// <summary>
/// Start-up, when Equalizer APO doesn't reach the sound card: says what is missing and does the next
/// step (download page, Configurator, or turning Windows' audio enhancements back on itself).
/// "Check again" re-reads the state; "Skip" carries on with the tray warning as before.
/// </summary>
public sealed class EqApoSetupDialog : Window
{
    private readonly string _deviceId;
    private readonly string _cardName;
    private readonly TextBlock _title = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _result = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
    private readonly Button _action = new() { MinWidth = 150, IsDefault = true };
    private EqApoSetupStep? _step;

    public EqApoStatus Status { get; private set; }

    private EqApoSetupDialog(string deviceId, string cardName, EqApoStatus status)
    {
        _deviceId = deviceId;
        _cardName = cardName;
        Title = "CabSound: Equalizer APO setup";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true; // at a cabinet boot it must not open behind Popper
        Loaded += (_, _) => Activate();

        var check = new Button { Content = "Check again", MinWidth = 110, Margin = new Thickness(8, 0, 0, 0) };
        var skip = new Button { Content = "Skip", IsCancel = true, MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
        _action.Click += (_, _) => DoStep();
        check.Click += (_, _) => Show(EqApoCheck.Status(_deviceId));
        skip.Click += (_, _) => DialogResult = false;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(_action);
        buttons.Children.Add(check);
        buttons.Children.Add(skip);

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(_title);
        panel.Children.Add(_text);
        panel.Children.Add(_result);
        panel.Children.Add(buttons);
        Content = panel;
        Show(status);
    }

    /// <summary>Shows the setup until Equalizer APO is active or the user skips. Returns the last status seen.</summary>
    public static EqApoStatus Run(string deviceId, string cardName, EqApoStatus status)
    {
        if (EqApoSetup.StepFor(status, cardName) is null) return status;
        var dialog = new EqApoSetupDialog(deviceId, cardName, status);
        dialog.ShowDialog();
        return dialog.Status;
    }

    private void Show(EqApoStatus status)
    {
        Status = status;
        _step = EqApoSetup.StepFor(status, _cardName);
        if (_step is null) { DialogResult = true; return; } // all good now
        _title.Text = _step.Title;
        _text.Text = _step.Text;
        _action.Content = _step.Button;
    }

    private void DoStep()
    {
        _result.Text = "";
        try
        {
            switch (_step?.Action)
            {
                case EqApoSetupAction.OpenDownloadPage:
                    Open(EqApoSetup.DownloadPage);
                    break;
                case EqApoSetupAction.OpenConfigurator:
                    Open(EqApoCheck.ConfiguratorPath);
                    break;
                case EqApoSetupAction.TurnOnEnhancements:
                    var done = EqApoFix.RunElevated(Environment.ProcessPath!, _deviceId);
                    Log.Write($"Turn on audio enhancements: {(done ? "done" : "failed or cancelled")}");
                    if (done) { Show(EqApoCheck.Status(_deviceId)); return; }
                    _result.Text = "That didn't work. Do it by hand: Windows Settings → System → Sound → "
                        + $"\"{_cardName}\" → Audio enhancements: Device Default Effects. Then click Check again.";
                    Open("ms-settings:sound");
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Write($"Equalizer APO setup: {e.Message}");
            _result.Text = e.Message;
        }
    }

    private static void Open(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
}
