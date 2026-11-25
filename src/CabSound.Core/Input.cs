namespace CabSound.Core;

public enum InputKind { Key, Joystick }

/// <summary>A keyboard key (Windows virtual-key code) or a joystick button (1-based, as PinOne reports it).</summary>
public readonly record struct InputId(InputKind Kind, int Code)
{
    public static InputId Key(int virtualKey) => new(InputKind.Key, virtualKey);
    public static InputId Joy(int button) => new(InputKind.Joystick, button);
    public override string ToString() => Kind == InputKind.Key ? $"Key 0x{Code:X2}" : $"Button {Code}";
}

public enum CabAction
{
    // Selected-group controls (simple layout)
    AdjustUp, AdjustDown, NextGroup,
    // Direct controls (PinVol-style layout)
    TableUp, TableDown, BackglassUp, BackglassDown, FrontUp, FrontDown, RearUp, RearDown, GlobalUp, GlobalDown,
    // Both layouts
    NightMode, Mute, OpenMenu,
}

/// <summary>
/// One input = single press, two inputs = chord (pressed together), Hold = held down.
/// HoldMs overrides the detector's default hold time for this binding (0 = use default).
/// Combo = PinVol-style key combo: the first inputs (e.g. Ctrl) must already be held when the
/// last one is pressed; the last key alone keeps its own single action, on press.
/// </summary>
public sealed record Binding(CabAction Action, IReadOnlyList<InputId> Inputs, bool Hold = false, int HoldMs = 0, bool Combo = false);
