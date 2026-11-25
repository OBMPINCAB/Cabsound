namespace CabSound.Core;

/// <summary>
/// PinOne sends a keyboard key AND a joystick button for one physical button
/// (e.g. left flipper = Left Shift + button 4). This learns those pairs from press timing
/// so a binding on the joystick button is also checked against its key.
/// </summary>
public sealed class ButtonTwins
{
    private const int SameButtonMs = 60;
    private readonly Dictionary<InputId, InputId> _twin = new();
    private (InputId Input, long Time)? _lastPress;

    public void Learn(InputId pressed, long timeMs)
    {
        if (_lastPress is { } last && timeMs - last.Time <= SameButtonMs && last.Input.Kind != pressed.Kind)
            Pair(last.Input, pressed);
        _lastPress = (pressed, timeMs);
    }

    public void Pair(InputId a, InputId b)
    {
        _twin[a] = b;
        _twin[b] = a;
    }

    /// <summary>The input plus its twin (joystick first).</summary>
    public IReadOnlyList<InputId> Expand(InputId input) =>
        _twin.TryGetValue(input, out var other)
            ? new[] { input, other }.OrderBy(i => i.Kind == InputKind.Joystick ? 0 : 1).ToArray()
            : new[] { input };
}
