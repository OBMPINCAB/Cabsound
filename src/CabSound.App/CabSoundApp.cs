using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CabSound.Core;
using CabSound.Platform;
using Forms = System.Windows.Forms;

namespace CabSound.App;

/// <summary>The running app: tray icon + menu, pop-up, engine and its threads.</summary>
public sealed class CabSoundApp
{
    private readonly CabSoundEngine _engine;
    private readonly EngineOutput _output;
    private readonly Workers _workers;
    private readonly Forms.NotifyIcon _tray;
    private readonly Forms.ToolStripMenuItem _status = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem _startWithWindows = new("Start with Windows");
    private DateTime _lastWarning = DateTime.MinValue;

    /// <param name="apo">Equalizer APO status; null when it couldn't be checked (no sound card found).</param>
    public CabSoundApp(CabSoundData data, EqApoStatus? apo, bool deviceFound)
    {
        _popup = new Popup(data.Overlays!.Popup);
        _popup.ShowHint(data.Overlays.ShowHint);
        _menuWindow = new MenuWindow(data.Overlays.Menu);
        _previewTimer.Tick += (_, _) => EndPreview();
        _output = new EngineOutput(data.DeviceId, data.DeviceName, _popup, Warn);
        _engine = new CabSoundEngine(data, _output);
        _workers = new Workers(_engine, _output.FixProgramVolumes);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open CabSound", null, (_, _) => OpenWindow());
        menu.Items.Add(_status);
        menu.Items.Add(new Forms.ToolStripSeparator());
        if (apo is EqApoStatus.NotOnDevice)
            menu.Items.Add("Fix Equalizer APO...", null, (_, _) => OpenConfigurator());
        _startWithWindows.Click += (_, _) => ToggleStartWithWindows();
        menu.Items.Add(_startWithWindows);
        menu.Items.Add(VolumeStepMenu(data.VolumeStep));
        menu.Items.Add("Open data folder", null, (_, _) => Process.Start("explorer.exe", Paths.DataDir));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        menu.Opening += (_, _) =>
        {
            _status.Text = _workers.Status;
            _startWithWindows.Checked = StartupPlan.IsEnabled(StartupFolder.Files(StartupFolder.Path));
        };

        _tray = new Forms.NotifyIcon { Icon = TrayIcon(), Text = "CabSound", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => OpenWindow();
        _workers.StateChanged += s => Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _state = s;
            _popup.Place(s.Overlays.Popup);
            _popup.ShowHint(s.Overlays.ShowHint);
            _menuWindow.Place(s.Overlays.Menu);
            if (_menu is not null) _menuWindow.Show(_menu, s);
            SyncSample();
            _window?.ShowState(s);
        });
        _workers.MenuChanged += m => Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _menu = m;
            if (m is null) _menuWindow.HideMenu();
            else if (_state is { } s) _menuWindow.Show(m, s);
            else _workers.Run((_, _) => { }); // no values yet: any work item publishes the state
            SyncSample();
        });

        if (!deviceFound) Warn($"Sound card '{data.DeviceName}' not found. Table volume won't change.");
        if (apo is { } status && EqApoHealth.Warning(status) is { } warning) Warn(warning);

        _workers.Start();
        Log.Write("Running");
    }

    private readonly Popup _popup;
    private readonly MenuWindow _menuWindow;
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _previewPopup, _sampleOn;

    /// <summary>
    /// Display tab "Preview": shows that window on its monitor for 5 s. Skipped while the real menu is
    /// open. The menu preview is a fresh read-only menu. UI thread.
    /// </summary>
    private void Preview(OverlayKind kind)
    {
        if (_menu is not null || _state is not { } s) return;
        if (kind == OverlayKind.Popup) _previewPopup = true;
        else _menuWindow.Show(new OverlayMenu(0), s);
        SyncSample();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void EndPreview()
    {
        _previewTimer.Stop();
        _previewPopup = false;
        if (_menu is null) _menuWindow.HideMenu();
        SyncSample();
    }

    /// <summary>The sample pop-up is up during a preview and while the menu's Display page edits the pop-up.</summary>
    private void SyncSample()
    {
        var want = _previewPopup || _menu is { Page: MenuPage.Display, EditingWindow: OverlayKind.Popup };
        if (want == _sampleOn) return;
        _sampleOn = want;
        _popup.ShowSample(want);
    }

    private MainWindow? _window;
    private EngineState? _state;   // latest engine state (UI thread)
    private OverlayMenu? _menu;    // open on-screen menu, null = closed (UI thread)

    /// <summary>The embedded "CS" icon at the tray size; the stock app icon if it is missing.</summary>
    private static Icon TrayIcon()
    {
        using var stream = typeof(CabSoundApp).Assembly.GetManifestResourceStream("CabSound.ico");
        return stream is null ? SystemIcons.Application : new Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    /// <summary>Shows the CabSound window (created once; closing only hides it) and asks the engine for its state.</summary>
    public void OpenWindow()
    {
        _window ??= new MainWindow(_workers, Preview);
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
        _workers.Run((_, _) => { }); // any work item publishes the state
    }

    /// <summary>Tray balloon; at most one a minute so a failing write can't flood the screen.</summary>
    private void Warn(string message) => Application.Current?.Dispatcher.BeginInvoke(() =>
    {
        if (DateTime.Now - _lastWarning < TimeSpan.FromMinutes(1)) return;
        _lastWarning = DateTime.Now;
        _tray.ShowBalloonTip(5000, "CabSound", message, Forms.ToolTipIcon.Warning);
    });

    /// <summary>Table volume step per press: 1 precise, 2 medium, 5 fast. Moves to Settings in step E.</summary>
    private Forms.ToolStripMenuItem VolumeStepMenu(int current)
    {
        var menu = new Forms.ToolStripMenuItem("Volume step");
        foreach (var (step, label) in new[] { (1, "1 (precise)"), (2, "2"), (5, "5 (fast)") })
        {
            var item = new Forms.ToolStripMenuItem(label) { Checked = step == current };
            item.Click += (_, _) =>
            {
                foreach (Forms.ToolStripMenuItem other in menu.DropDownItems) other.Checked = other == item;
                _workers.SetVolumeStep(step);
                Log.Write($"Volume step: {step}");
            };
            menu.DropDownItems.Add(item);
        }
        return menu;
    }

    private void ToggleStartWithWindows()
    {
        try
        {
            var startup = StartupFolder.Files(StartupFolder.Path);
            var parked = StartupFolder.Files(Paths.ParkedStartup);
            var steps = StartupPlan.IsEnabled(startup) ? StartupPlan.Disable(startup, parked) : StartupPlan.Enable(startup, parked);
            StartupFolder.Apply(steps, Paths.ParkedStartup, Environment.ProcessPath!);
            Log.Write($"Start with Windows: {string.Join(", ", steps)}");
        }
        catch (Exception e) { Log.Write($"Start with Windows failed: {e}"); Warn($"Start with Windows failed: {e.Message}"); }
    }

    private void Exit()
    {
        Log.Write("Exit");
        Shutdown();
    }

    /// <summary>Stops the threads (pending levels are written) and removes the tray icon. Safe to call twice.</summary>
    public void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        _previewTimer.Stop();
        _workers.Stop();
        _output.Dispose();
        _popup.Close();
        _menuWindow.Close();
        _window?.CloseForGood();
        _tray.Visible = false;
        _tray.Dispose();
    }

    private bool _stopped;

    private void Shutdown()
    {
        Stop();
        Application.Current.Shutdown();
    }

    private static void OpenConfigurator()
    {
        try { Process.Start(new ProcessStartInfo(EqApoCheck.ConfiguratorPath) { UseShellExecute = true }); }
        catch (Exception e) { Log.Write($"Configurator: {e.Message}"); }
    }
}
