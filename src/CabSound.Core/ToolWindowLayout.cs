namespace CabSound.Core;

/// <summary>
/// Where the three cabinet windows (speaker check, calibration wizard, exciter limit check) open and how
/// they turn: rotation in steps of 90°, the saved place when it is still on a screen, else the centre of the
/// main screen. Pure sums; the App's ToolWindowFrame does the Windows part.
/// </summary>
public static class ToolWindowLayout
{
    /// <summary>The saved rotation; upright when never set or not one of 0 / 90 / 180 / 270 (hand-edited file).</summary>
    public static int Rotation(ToolWindowPlace? saved) => saved is { Rotation: 90 or 180 or 270 } ? saved.Rotation : 0;

    /// <summary>90° further clockwise; after 270 upright again. An unknown value counts as upright.</summary>
    public static int NextRotation(int rotation) => rotation switch { 90 => 180, 180 => 270, 270 => 0, _ => 90 };

    /// <summary>
    /// The window's top-left for its outline as shown: the saved place when the outline's centre is on one of
    /// the screens, else centred on the main screen (the first one when none is marked; 0, 0 without screens).
    /// </summary>
    public static (int X, int Y) TopLeft(ToolWindowPlace? saved, int width, int height, IReadOnlyList<MonitorInfo> monitors)
    {
        if (saved is not null && monitors.Any(m => Holds(m, saved.X + width / 2, saved.Y + height / 2))) return (saved.X, saved.Y);
        var main = monitors.FirstOrDefault(m => m.Primary) ?? monitors.FirstOrDefault();
        return main is null ? (0, 0) : (main.X + (main.Width - width) / 2, main.Y + (main.Height - height) / 2);
    }

    /// <summary>The new top-left when a width × height outline becomes height × width around the same centre.</summary>
    public static (int X, int Y) AfterTurn(int x, int y, int width, int height) =>
        (x + (width - height) / 2, y + (height - width) / 2);

    /// <summary>
    /// The new top-left when the outline changes size (a step shows more or less), so the bar stays put under
    /// the owner's hand. The bar is the content's top: 0° = window top, 90° = right edge, 180° = bottom, 270° = left.
    /// </summary>
    public static (int X, int Y) AfterResize(int rotation, int x, int y, int oldWidth, int oldHeight, int newWidth, int newHeight) => rotation switch
    {
        90 => (x - (newWidth - oldWidth), y),
        180 => (x, y - (newHeight - oldHeight)),
        _ => (x, y),
    };

    private static bool Holds(MonitorInfo m, int x, int y) => x >= m.X && x < m.X + m.Width && y >= m.Y && y < m.Y + m.Height;
}
