using System;
using System.Threading;
using System.Windows;
using CabSound.Core;
using CabSound.Platform;

namespace CabSound.App;

/// <summary>Tray app: no main window. Start-up flow in StartupFlow, running pieces in CabSoundApp.</summary>
public partial class App : Application
{
    private const string ShowEventName = "CabSound.App.Show";
    private Mutex? _single;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private CabSoundApp? _app;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Elevated helper run (from the Equalizer APO setup): turn enhancements on for one card, then exit
        if (EqApoSetup.DeviceToFix(e.Args) is { } deviceId)
        {
            Log.Init(AppContext.BaseDirectory);
            var error = EqApoFix.TurnOnEnhancements(deviceId, Paths.Backups, DateTime.Now);
            Log.Write($"Helper: audio enhancements {(error is null ? "turned on" : $"not changed: {error}")}");
            Shutdown(error is null ? 0 : 1);
            return;
        }

        _single =new Mutex(true, "CabSound.App", out var first);
        if (!first)
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
            {
                using (existing) existing.Set();
            }
            Shutdown();
            return;
        }

        Log.Init(AppContext.BaseDirectory);
        DispatcherUnhandledException += (_, ex) => { Log.Write($"UI error: {ex.Exception}"); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Write($"Fatal: {ex.ExceptionObject}");

        _app = StartupFlow.Run();
        if (_app is null) { Shutdown(); return; }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) =>
            Dispatcher.BeginInvoke(() => _app?.OpenWindow()), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    // Windows shutdown/log off is how a cabinet normally ends CabSound: save pending levels first
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Log.Write($"Windows {e.ReasonSessionEnding}");
        _app?.Stop();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _app?.Stop();
        _single?.Dispose();
        base.OnExit(e);
    }
}
