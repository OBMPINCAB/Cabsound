namespace CabSound.Core;

/// <summary>
/// Button presses → actions, with hold-to-repeat. Actions fired on press or by a hold repeat
/// while the button stays down; actions fired on release (tap of a button that also has a hold)
/// happen once.
/// </summary>
public sealed class ButtonPresses(IEnumerable<Binding> bindings, int holdMs, RepeatTimer<CabAction>? repeat = null)
{
    private readonly List<Binding> _bindings = bindings.ToList();
    private readonly GestureDetector _gestures = new(bindings, holdMs);
    private readonly RepeatTimer<CabAction> _repeat = repeat ?? new RepeatTimer<CabAction>();
    private readonly Dictionary<InputId, List<CabAction>> _repeating = new();

    public IReadOnlyList<CabAction> Down(InputId input, long nowMs)
    {
        var fired = _gestures.Down(input, nowMs);
        foreach (var a in fired.Where(a => a != CabAction.OpenMenu)) StartRepeat(input, a, nowMs);
        return fired;
    }

    public IReadOnlyList<CabAction> Up(InputId input, long nowMs)
    {
        if (_repeating.Remove(input, out var actions))
            foreach (var a in actions) _repeat.Stop(a);
        return _gestures.Up(input, nowMs);
    }

    public IReadOnlyList<CabAction> Tick(long nowMs)
    {
        var fired = new List<CabAction>(_repeat.Tick(nowMs));
        foreach (var a in _gestures.Tick(nowMs))
        {
            fired.Add(a);
            if (a == CabAction.OpenMenu) continue;
            foreach (var input in _bindings.First(b => b.Hold && b.Action == a).Inputs) StartRepeat(input, a, nowMs);
        }
        return fired;
    }

    private void StartRepeat(InputId input, CabAction action, long nowMs)
    {
        _repeat.Start(action, nowMs);
        if (!_repeating.TryGetValue(input, out var list)) _repeating[input] = list = [];
        list.Add(action);
    }
}
