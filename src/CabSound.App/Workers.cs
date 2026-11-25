using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CabSound.Core;
using CabSound.Platform;

namespace CabSound.App;

/// <summary>
/// Three background threads. Input: reads configured keys/joystick buttons every 20 ms and queues actions.
/// Table: asks Windows which table runs every 2 s and queues changes. Engine: the only thread that
/// touches the engine (actions, ticks, file writes); flushes pending writes when stopping.
/// </summary>
/// <param name="checkPrograms">Called by the engine thread every 2 s (Windows' volume mixer check); null = none.</param>
public sealed class Workers(CabSoundEngine engine, Action? checkPrograms = null)
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Thread> _threads = [];

    /// <summary>Current table and levels for the tray menu (updated by the engine thread).</summary>
    public volatile string Status = "Starting...";

    /// <summary>Latest control settings (published by engine thread, read by input thread).</summary>
    private volatile ControlsSettings _controls = engine.State.ControlsOrDefault;

    private sealed record CaptureRequest(bool AllowCombo, bool AllowJoystick, Action<IReadOnlyList<InputId>?> Callback);
    private volatile CaptureRequest? _capture;

    /// <summary>
    /// Captures the next key, modifier+key combo, or joystick button for the Controls tab.
    /// Pressing Esc cancels (callback receives null). Normal hotkeys pause while capturing.
    /// </summary>
    public void BeginCapture(bool allowCombo, bool allowJoystick, Action<IReadOnlyList<InputId>?> onCaptured) =>
        _capture = new CaptureRequest(allowCombo, allowJoystick, onCaptured);

    public void CancelCapture() => _capture = null;

    public void Start()
    {
        foreach (var (name, loop) in new (string, Action)[] { ("Input", InputLoop), ("Table", TableLoop), ("Engine", EngineLoop) })
        {
            var t = new Thread(() => Guard(name, loop)) { IsBackground = true, Name = $"CabSound {name}" };
            _threads.Add(t);
            t.Start();
        }
    }

    /// <summary>
    /// Stops all threads; the engine thread runs queued presses and writes anything pending first.
    /// Returns false if a thread didn't stop in time (the engine may still be running).
    /// </summary>
    public bool Stop()
    {
        _capture = null;
        _stop.Cancel();
        var clean = true;
        foreach (var t in _threads)
            if (!t.Join(5000)) { clean = false; Log.Write($"{t.Name} didn't stop within 5 s"); }
        return clean;
    }

    private static long Now => Environment.TickCount64;

    /// <summary>Raised on the input thread when the on-screen menu opens or changes; null = closed.</summary>
    public event Action<OverlayMenu?>? MenuChanged;

    /// <summary>Latest running table key from the table thread (read by the input thread).</summary>
    private volatile string _tableKey = "System";

    // On-screen menu; input thread only. Keys/hold-back/session are null when menu keys aren't available.
    private MenuKeys? _menuKeys;
    private MenuHoldBack? _holdBack;
    private MenuSession? _session;
    private OverlayMenu? _menu;
    private GuideBookmark _guideBookmark;
    private string _menuTable = "";
    private bool _menuKeysMissingLogged;
    private bool _pauseTapped; // CabSound really sent the open tap, so it must send the resume tap

    private void InputLoop()
    {
        var activeControls = _controls;
        var vpxMenuKeys = ReadVpxMenuKeys();
        var presses = new ButtonPresses(activeControls.ToRuntimeBindings(), 2000);
        ApplyMenuKeys(activeControls.ResolveMenuKeys(vpxMenuKeys));

        var keys = new ConcurrentQueue<(int Vk, bool Down)>();
        var joyEvents = new ConcurrentQueue<(int Button, bool Down)>();
        var joystick = new JoystickReader();
        joystick.Button += (_, btn, down) => joyEvents.Enqueue((btn, down));

        using var keyboard = new KeyboardWatcher
        {
            // Volume keys are CabSound's: otherwise Windows changes the volume as well.
            // Menu keys: Decide sees every down/up (menu open or not) so each press stays paired.
            HoldBack = (vk, down) => ControlLayouts.IsVolumeKey(vk) || _holdBack?.Decide(vk, down) == true,
        };
        keyboard.Key += (vk, down, _) => keys.Enqueue((vk, down));
        keyboard.Start();
        Log.Write($"Listening to controls ({activeControls.Layout})");

        var heldModifiers = new HashSet<int>();
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                // Live reload only when controls actually changed on the Controls tab
                var latest = _controls;
                if (!latest.SameAs(activeControls))
                {
                    if (_menu is not null) CloseMenu();
                    activeControls = latest;
                    presses = new ButtonPresses(activeControls.ToRuntimeBindings(), 2000);
                    ApplyMenuKeys(activeControls.ResolveMenuKeys(vpxMenuKeys));
                    _menuKeysMissingLogged = false;
                    Log.Write($"Controls updated ({activeControls.Layout})");
                }

                var cap = _capture;
                if (activeControls.UsesJoystick() || cap is { AllowJoystick: true })
                    joystick.Poll();

                while (keys.TryDequeue(out var k))
                {
                    if (InputNames.IsModifier(k.Vk))
                    {
                        if (k.Down) heldModifiers.Add(k.Vk);
                        else heldModifiers.Remove(k.Vk);
                    }

                    cap = _capture;
                    if (cap is not null)
                    {
                        if (k.Down) HandleKeyCapture(cap, k.Vk, heldModifiers);
                        continue;
                    }

                    var now = Now;
                    var menuWasOpen = _menu is not null;
                    // Every key, before any OpenMenu, so the opening keys are already known as down
                    if (_session is not null) MenuSignals(_session.Key(k.Vk, k.Down, now), now);

                    var input = InputId.Key(k.Vk);
                    if (menuWasOpen)
                    {
                        // Menu presses aren't hotkeys; releases still go in so nothing stays "down"
                        if (!k.Down) presses.Up(input, now);
                        continue;
                    }
                    foreach (var a in k.Down ? presses.Down(input, now) : presses.Up(input, now)) Fire(a, now);
                }

                while (joyEvents.TryDequeue(out var j))
                {
                    cap = _capture;
                    if (cap is not null)
                    {
                        if (j.Down && cap.AllowJoystick)
                        {
                            _capture = null;
                            cap.Callback([InputId.Joy(j.Button)]);
                        }
                        continue;
                    }

                    var now = Now;
                    var input = InputId.Joy(j.Button);
                    if (_menu is not null)
                    {
                        if (!j.Down) presses.Up(input, now);
                        continue;
                    }
                    foreach (var a in j.Down ? presses.Down(input, now) : presses.Up(input, now)) Fire(a, now);
                }

                var tick = Now;
                if (_capture is null)
                {
                    var repeats = presses.Tick(tick);
                    if (_menu is null) foreach (var a in repeats) Fire(a, tick);
                    else MenuTick(tick);
                }
            }
            catch (Exception e) { Log.Write($"Input: {e.Message}"); }
            Thread.Sleep(20);
        }

        try { if (_menu is not null) CloseMenu(); } // CabSound is quitting: keys must not stay held back, the table must resume
        catch (Exception e) { Log.Write($"Closing menu: {e.Message}"); }
    }

    private void HandleKeyCapture(CaptureRequest cap, int vk, HashSet<int> heldModifiers)
    {
        if (vk == InputNames.VkEscape)
        {
            _capture = null;
            cap.Callback(null);
            return;
        }
        if (InputNames.IsModifier(vk))
        {
            // When binding single menu keys (no combo), allow binding a modifier key itself (e.g., Left Shift / Left Ctrl)
            if (!cap.AllowCombo)
            {
                _capture = null;
                cap.Callback([InputId.Key(vk)]);
            }
            return;
        }
        _capture = null;
        if (cap.AllowCombo && heldModifiers.Count > 0)
        {
            var mod = heldModifiers.First();
            cap.Callback([InputId.Key(mod), InputId.Key(vk)]);
        }
        else
        {
            cap.Callback([InputId.Key(vk)]);
        }
    }

    private void ApplyMenuKeys(MenuKeys? keys)
    {
        _menuKeys = keys;
        if (keys is not null)
        {
            _holdBack = new MenuHoldBack(keys);
            _session = new MenuSession(keys);
        }
        else
        {
            _holdBack = null;
            _session = null;
        }
    }

    /// <summary>VPX's flipper/magna/Start/Pause keys from VPinballX.ini (read-only); null if not found.</summary>
    public static MenuKeys? ReadVpxMenuKeys()
    {
        try
        {
            var ini = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VPinballX", "VPinballX.ini");
            return File.Exists(ini) ? MenuKeys.FromVpx(ConflictChecker.FromVpxIni(File.ReadAllText(ini))) : null;
        }
        catch (Exception e)
        {
            Log.Write($"Reading VPX keys: {e.Message}");
            return null;
        }
    }

    /// <summary>Reads reserved keys/buttons from VPX and PinUP Popper (if installed) for conflict checking.</summary>
    public static List<ReservedInput> ReadReservedInputs()
    {
        var list = new List<ReservedInput>();
        try
        {
            var ini = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VPinballX", "VPinballX.ini");
            if (File.Exists(ini)) list.AddRange(ConflictChecker.FromVpxIni(File.ReadAllText(ini)));
        }
        catch (Exception e) { Log.Write($"Reading VPX reserved inputs: {e.Message}"); }

        try
        {
            if (File.Exists(PopperControls.DefaultDb)) list.AddRange(ConflictChecker.FromPopper(PopperControls.Read()));
        }
        catch (Exception e) { Log.Write($"Reading Popper reserved inputs: {e.Message}"); }

        return list;
    }

    /// <summary>A hotkey action: OpenMenu opens the menu here, the rest go to the engine.</summary>
    private void Fire(CabAction action, long now)
    {
        if (action == CabAction.OpenMenu) OpenMenu(now);
        else Queue(action);
    }

    private void OpenMenu(long now)
    {
        if (_session is null)
        {
            if (!_menuKeysMissingLogged) Log.Write("Menu keys not found (check Controls tab)");
            _menuKeysMissingLogged = true;
            return;
        }
        if (_session.IsOpen) return;

        _menu = new OverlayMenu(now, _guideBookmark);
        _menuTable = _tableKey;
        _pauseTapped = false;
        _holdBack!.SetActive(true);
        Log.Write("Menu opened");
        MenuChanged?.Invoke(_menu);
        MenuSignals(_session.Open(_menuTable.StartsWith("VP."), now), now);
    }

    /// <summary>Idle timeout, reset-confirm expiry and table change.</summary>
    private void MenuTick(long now)
    {
        if (_tableKey != _menuTable) { CloseMenu(tableStillRunning: false); return; } // before any idle close: a gone table isn't paused
        if (_menu is not { } menu) return;

        var armed = menu.ResetArmed;
        menu.Tick(now);
        if (menu.IsClosed) CloseMenu();
        else if (menu.ResetArmed != armed) MenuChanged?.Invoke(menu);
    }

    /// <summary>Acts on the session's signals; ends the menu when the session or the menu has closed.</summary>
    private void MenuSignals(IReadOnlyList<MenuSignal> signals, long now)
    {
        try
        {
            foreach (var s in signals)
            {
                if (s.Kind == MenuSignalKind.TapPause) PauseSignal();
                else if (s.Kind == MenuSignalKind.Command && _menu?.Handle(s.Command, now) is { } effect)
                {
                    var monitors = Monitors.All();
                    Run((e, t) => e.ApplyMenu(effect, monitors, t));
                }
            }
        }
        catch (Exception e) { Log.Write($"Menu: {e.Message}"); }
        finally
        {
            // Even after an error: a closed session or menu must end the menu (hold-back off, table resumed)
            if (_menu is not null && signals.Count > 0)
            {
                if (!_session!.IsOpen || _menu.IsClosed) CloseMenu();
                else MenuChanged?.Invoke(_menu);
            }
        }
    }

    /// <summary>The session wants a pause tap: while it is open that pauses the table, after closing it resumes it.</summary>
    private void PauseSignal()
    {
        if (_session!.IsOpen) _pauseTapped = TapPauseInVpx();
        else if (_pauseTapped) { _pauseTapped = false; TapPauseInVpx(); } // resume only what we paused
    }

    /// <summary>Taps VPX's pause key, but only if VPX has the focus (else the key would go to another program).</summary>
    private bool TapPauseInVpx()
    {
        if (!ForegroundApp.IsVpx())
        {
            Log.Write("Pause key not sent: VPX isn't the foreground window");
            return false;
        }
        KeySender.Tap(_menuKeys!.Pause);
        return true;
    }

    /// <summary>
    /// Closes the session (resumes the table if it paused it; a Close signal already did), stops holding
    /// keys back and hides the menu. Hold-back goes off first, so an error can't leave keys held back.
    /// </summary>
    private void CloseMenu(bool tableStillRunning = true)
    {
        if (_menu is not null) _guideBookmark = _menu.Bookmark;
        var resume = _session!.Close(tableStillRunning);
        _menu = null;
        _holdBack!.SetActive(false);
        Log.Write("Menu closed");
        foreach (var s in resume)
            if (s.Kind == MenuSignalKind.TapPause) PauseSignal();
        _pauseTapped = false;
        MenuChanged?.Invoke(null);
    }

    private void TableLoop()
    {
        string? last = null;
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                var key = TableWatcher.CurrentKey();
                if (key != last)
                {
                    last = key;
                    _tableKey = key;
                    _queue.Add(() => { engine.TableChanged(key, Now); Log.Write($"Table: {key}"); });
                }
            }
            catch (Exception e) { Log.Write($"Table: {e.Message}"); }
            _stop.Token.WaitHandle.WaitOne(2000);
        }
    }

    private void EngineLoop()
    {
        long nextProgramCheck = 0;
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                if (_queue.TryTake(out var work, 50)) { work(); UpdateStatus(); }
                engine.Tick(Now);
                // Not queued: queued work publishes the state, and this changes none of it
                if (checkPrograms is not null && Now >= nextProgramCheck)
                {
                    nextProgramCheck = Now + 2000;
                    checkPrograms();
                }
            }
            catch (Exception e) { Log.Write($"Engine: {e}"); }
        }
        // Presses queued just before stopping still count
        while (_queue.TryTake(out var late))
        {
            try { late(); }
            catch (Exception e) { Log.Write($"Engine: {e}"); }
        }
        try { engine.Flush(); }
        catch (Exception e) { Log.Write($"Flush: {e}"); }
    }

    private void Queue(CabAction action) => _queue.Add(() => engine.Handle(action, Now));

    /// <summary>Changes the volume step on the engine thread (from the tray menu).</summary>
    public void SetVolumeStep(int step) => _queue.Add(() => engine.SetVolumeStep(step, Now));

    /// <summary>Runs window work on the engine thread (the only thread that touches the engine); gets the time.</summary>
    public void Run(Action<CabSoundEngine, long> work) => _queue.Add(() => work(engine, Now));

    /// <summary>Raised on the engine thread after every change (the window marshals it to the UI thread).</summary>
    public event Action<EngineState>? StateChanged;

    private void UpdateStatus()
    {
        var state = engine.State;
        _controls = state.ControlsOrDefault;
        var t = engine.Current;
        Status = $"{t.Name}: volume {t.Volume}, backbox {t.BackglassDb:+0;-0;0} dB, " +
                 $"front exciters {t.FrontDb:+0;-0;0} dB, rear exciters {t.RearDb:+0;-0;0} dB";
        StateChanged?.Invoke(state);
    }

    private static void Guard(string name, Action loop)
    {
        try { loop(); }
        catch (Exception e) { Log.Write($"{name} thread stopped: {e}"); }
    }
}
