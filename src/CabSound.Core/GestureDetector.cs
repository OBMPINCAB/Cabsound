namespace CabSound.Core;

/// <summary>
/// Turns raw button presses into actions: single press, chord (two together), hold and
/// PinVol-style combo (Ctrl held, then key). A button that is also part of a chord or hold fires
/// its single action on release, and only if no chord or hold fired while it was down.
/// A combo doesn't delay the key's single action: the modifier decides, at the moment of the press.
/// </summary>
public sealed class GestureDetector
{
    private readonly List<Binding> _bindings;
    private readonly int _holdMs;
    private readonly Dictionary<InputId, long> _downSince = new();
    private readonly HashSet<InputId> _consumed = new();

    public GestureDetector(IEnumerable<Binding> bindings, int holdMs)
    {
        _bindings = bindings.ToList();
        _holdMs = holdMs;
    }

    public IReadOnlyList<CabAction> Down(InputId input, long timeMs)
    {
        if (_downSince.ContainsKey(input)) return [];
        _downSince[input] = timeMs;

        var combo = _bindings.FirstOrDefault(b => b.Combo && b.Inputs[^1] == input
            && b.Inputs.Take(b.Inputs.Count - 1).All(_downSince.ContainsKey));
        if (combo is not null)
        {
            _consumed.Add(input); // the modifier stays usable for the next press
            return [combo.Action];
        }

        var chord = _bindings.FirstOrDefault(b => !b.Hold && !b.Combo && b.Inputs.Count > 1
            && b.Inputs.Contains(input) && b.Inputs.All(_downSince.ContainsKey));
        if (chord is not null)
        {
            foreach (var i in chord.Inputs) _consumed.Add(i);
            return [chord.Action];
        }

        var single = Single(input);
        return single is not null && !IsShared(input) ? [single.Action] : [];
    }

    public IReadOnlyList<CabAction> Up(InputId input, long timeMs)
    {
        if (!_downSince.Remove(input)) return [];
        var wasConsumed = _consumed.Remove(input);

        var single = Single(input);
        return single is not null && IsShared(input) && !wasConsumed ? [single.Action] : [];
    }

    public IReadOnlyList<CabAction> Tick(long timeMs)
    {
        var fired = new List<CabAction>();
        foreach (var b in _bindings.Where(b => b.Hold))
        {
            var holdMs = b.HoldMs > 0 ? b.HoldMs : _holdMs;
            var held = b.Inputs.All(i => _downSince.TryGetValue(i, out var t) && timeMs - t >= holdMs);
            if (held && !b.Inputs.Any(_consumed.Contains))
            {
                foreach (var i in b.Inputs) _consumed.Add(i);
                fired.Add(b.Action);
            }
        }
        return fired;
    }

    private Binding? Single(InputId input) =>
        _bindings.FirstOrDefault(b => !b.Hold && !b.Combo && b.Inputs.Count == 1 && b.Inputs[0] == input);

    private bool IsShared(InputId input) =>
        _bindings.Any(b => !b.Combo && b.Inputs.Contains(input) && (b.Hold || b.Inputs.Count > 1));
}
