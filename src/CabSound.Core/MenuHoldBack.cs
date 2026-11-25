namespace CabSound.Core;

/// <summary>
/// Decides, on the keyboard-hook thread, which menu keys are held back from VPX while the menu is open.
/// Keeps each key's down and up together: a key is held back only if its down was, and a key that was
/// already down when the menu opened is never held back (VPX saw its down, so it must see its up).
/// A lost key-up doesn't stick: Windows only auto-repeats the key pressed last, so a "down" for a key
/// still marked down after another key was pressed is a new press and is decided afresh.
/// </summary>
public sealed class MenuHoldBack(MenuKeys keys)
{
    private readonly object _lock = new();
    private readonly Dictionary<int, bool> _down = []; // menu keys physically down: true = held back, false = passing
    private int _lastDown;                             // any key, menu key or not
    private bool _active;

    public void SetActive(bool active)
    {
        lock (_lock) _active = active;
    }

    /// <summary>True when this key event must be held back from other programs.</summary>
    public bool Decide(int vk, bool down)
    {
        var menuKey = keys.ShouldHoldBack(vk);

        lock (_lock)
        {
            if (down)
            {
                var autoRepeat = vk == _lastDown;
                _lastDown = vk;
                if (!menuKey) return false;
                if (autoRepeat && _down.TryGetValue(vk, out var held)) return held;
                _down[vk] = _active;
                return _active;
            }

            return menuKey && _down.Remove(vk, out var wasHeld) && wasHeld;
        }
    }
}
