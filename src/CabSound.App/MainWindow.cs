using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CabSound.Core;

namespace CabSound.App;

/// <summary>The CabSound window (desktop monitor): Now Playing, Tables, Sound, Controls, Display. Closing only hides it.</summary>
public sealed class MainWindow : Window
{
    private readonly NowPlayingView _nowPlaying;
    private readonly TablesView _tables;
    private readonly SoundView _sound;
    private readonly ControlsView _controls;
    private readonly DisplayView _display;
    private bool _closing;

    public MainWindow(Workers workers, Action<OverlayKind> previewOverlay)
    {
        Title = "CabSound";
        Width = 920;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _nowPlaying = new NowPlayingView(workers);
        _tables = new TablesView(workers);
        _sound = new SoundView(workers);
        _controls = new ControlsView(workers);
        _display = new DisplayView(workers, previewOverlay);
        Content = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Now Playing", Content = new ScrollViewer { Content = _nowPlaying } },
                new TabItem { Header = "Tables", Content = _tables },
                new TabItem { Header = "Sound", Content = new ScrollViewer { Content = _sound } },
                new TabItem { Header = "Controls", Content = new ScrollViewer { Content = _controls } },
                new TabItem { Header = "Display", Content = new ScrollViewer { Content = _display } },
            },
        };
    }

    public void ShowState(EngineState s)
    {
        _nowPlaying.ShowState(s);
        _tables.ShowState(s);
        _sound.ShowState(s);
        _controls.ShowState(s);
        _display.ShowState(s);
    }

    /// <summary>Really closes (app exit).</summary>
    public void CloseForGood()
    {
        _controls.CancelCapture();
        _closing = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _controls.CancelCapture();
        if (!_closing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
