namespace CabSound.Core;

public enum MenuSignalKind { Command, TapPause, Close }

public readonly record struct MenuSignal(MenuSignalKind Kind, MenuCommand Command = default);

/// <summary>
/// Input while the on-screen menu is open: which presses are menu commands, when to tap VPX's Pause
/// key, and when the menu closes itself. Feed it every key, open or not, so it knows what is already down.
/// A held key repeats on Windows' own key repeat, not on a timer: Windows can lose key-ups (both flippers
/// are Shift keys; with both pressed neither key-up arrived on the cabinet), and its key repeat stops
/// when the key is let go either way.
/// </summary>
public sealed class MenuSession(MenuKeys keys, int repeatMs = 100)
{
    private const int VolumeUp = 0xAF, LeftCtrl = 0xA2, RightCtrl = 0xA3;
    private static readonly MenuSignal TapPause = new(MenuSignalKind.TapPause);
    private static readonly MenuSignal CloseSignal = new(MenuSignalKind.Close);

    private readonly HashSet<int> _down = [];
    private int _lastDown;        // the key pressed last: the only one Windows repeats
    private long _nextRepeatMs;   // Windows repeats faster than the menu should move
    private readonly HashSet<int> _heldAtOpen = []; // the opening keys: ignored until released
    private bool _pausePending;   // wanted a pause but Ctrl is still down
    private bool _pauseTapped;    // a pause was sent, so closing must resume

    public bool IsOpen { get; private set; }

    public IReadOnlyList<MenuSignal> Open(bool pauseTable, long nowMs)
    {
        IsOpen = true;
        _heldAtOpen.Clear();
        _heldAtOpen.UnionWith(_down);
        _pauseTapped = false;
        _pausePending = pauseTable;
        return TryPause();
    }

    public IReadOnlyList<MenuSignal> Key(int vk, bool down, long nowMs)
    {
        if (!down)
        {
            _down.Remove(vk);
            _heldAtOpen.Remove(vk);
            return IsOpen ? TryPause() : [];
        }

        // A "down" for the key pressed last that is still marked down is Windows' key repeat (or a new
        // press whose key-up was lost: it moves the menu the same way). For any other key it is a new press.
        var keyRepeat = !_down.Add(vk) && vk == _lastDown;
        _lastDown = vk;
        if (!IsOpen || _heldAtOpen.Contains(vk)) return [];

        var command = keys.ToCommand(vk);
        if (keyRepeat)
        {
            if (command is null or MenuCommand.Close || nowMs < _nextRepeatMs) return [];
            _nextRepeatMs = nowMs + repeatMs;
            return [new MenuSignal(MenuSignalKind.Command, command.Value)];
        }

        if (vk == VolumeUp || command == MenuCommand.Close) return CloseNow([CloseSignal], true);
        if (command is null) return [];

        _nextRepeatMs = nowMs + repeatMs;
        return [new MenuSignal(MenuSignalKind.Command, command.Value)];
    }

    public IReadOnlyList<MenuSignal> Close(bool tableStillRunning = true) =>
        IsOpen ? CloseNow([], tableStillRunning) : [];

    private IReadOnlyList<MenuSignal> TryPause()
    {
        if (!_pausePending || _down.Contains(LeftCtrl) || _down.Contains(RightCtrl)) return [];
        _pausePending = false;
        _pauseTapped = true;
        return [TapPause];
    }

    private IReadOnlyList<MenuSignal> CloseNow(List<MenuSignal> signals, bool tableStillRunning)
    {
        if (_pauseTapped && tableStillRunning) signals.Add(TapPause);
        IsOpen = false;
        _pausePending = false;
        _pauseTapped = false;
        return signals;
    }
}
