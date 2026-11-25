namespace CabSound.Core;

/// <summary>Pure math: turns an overlay placement into a pixel rectangle; migrates the old PinVol box.</summary>
public static class OverlayLayout
{
    public readonly record struct PixelRect(int X, int Y, int Width, int Height);

    static readonly MonitorInfo StandIn = new("", 0, 0, 1920, 1080, true);

    /// <summary>By name, else the primary monitor, else the first; no monitors = a 1920x1080 stand-in.</summary>
    public static MonitorInfo FindMonitor(string? name, IReadOnlyList<MonitorInfo> monitors)
    {
        if (name is not null && monitors.FirstOrDefault(m => m.Name == name) is { } named) return named;
        return monitors.FirstOrDefault(m => m.Primary) ?? monitors.FirstOrDefault() ?? StandIn;
    }

    /// <summary>The pixel box for a placement; always kept on its monitor (pinned top/left if too big).</summary>
    public static PixelRect Place(OverlayPlacement p, IReadOnlyList<MonitorInfo> monitors)
    {
        var m = FindMonitor(p.Monitor, monitors);
        var w = (int)Math.Round(p.Width * p.SizePercent / 100.0);
        var h = (int)Math.Round(p.Height * p.SizePercent / 100.0);
        var turned = p.Rotation is 90 or 270;
        var (sw, sh) = turned ? (h, w) : (w, h);

        var (ux, uy) = Up(p.Rotation);
        var (rx, ry) = (-uy, ux);

        var x = m.X + (m.Width - sw) / 2;
        var y = m.Y + (m.Height - sh) / 2;

        if (p.Position == OverlayPosition.LockdownBar)
        {
            // edge opposite "up"
            if (ux == -1) x = m.X + m.Width - sw; else if (ux == 1) x = m.X;
            if (uy == -1) y = m.Y + m.Height - sh; else if (uy == 1) y = m.Y;
        }
        else if (p.Position == OverlayPosition.Backbox)
        {
            // edge "up" points to
            if (ux == 1) x = m.X + m.Width - sw; else if (ux == -1) x = m.X;
            if (uy == 1) y = m.Y + m.Height - sh; else if (uy == -1) y = m.Y;
        }

        x += p.Toward * ux + p.Along * rx;
        y += p.Toward * uy + p.Along * ry;

        x = Math.Max(m.X, Math.Min(x, m.X + m.Width - sw));
        y = Math.Max(m.Y, Math.Min(y, m.Y + m.Height - sh));
        return new PixelRect(x, y, sw, sh);
    }

    /// <summary>Old PinVol pixel box to overlay settings, so the pop-up lands on the same pixels.</summary>
    public static OverlaySettings FromPinVolBox(DisplaySettings box, IReadOnlyList<MonitorInfo> monitors)
    {
        var cx = box.X + box.Width / 2;
        var cy = box.Y + box.Height / 2;
        var m = monitors.FirstOrDefault(k => cx >= k.X && cx < k.X + k.Width && cy >= k.Y && cy < k.Y + k.Height)
                ?? FindMonitor(null, monitors);

        var turned = box.Rotation is 90 or 270;
        var (w, h) = turned ? (box.Height, box.Width) : (box.Width, box.Height);

        var basePlacement = new OverlayPlacement(m.Name, box.Rotation, OverlayPosition.LockdownBar, 0, 0, w, h, 100, 80);
        var origin = Place(basePlacement, monitors);
        var (dx, dy) = (box.X - origin.X, box.Y - origin.Y);
        var (ux, uy) = Up(box.Rotation);
        var (rx, ry) = (-uy, ux);

        var popup = basePlacement with { Toward = dx * ux + dy * uy, Along = dx * rx + dy * ry };
        var menu = new OverlayPlacement(m.Name, box.Rotation, OverlayPosition.Middle, 0, 0, 900, 700, 100, 85);
        return new OverlaySettings(popup, menu);
    }

    /// <summary>Default placement when there is no PinVol box to migrate.</summary>
    public static OverlaySettings Defaults(IReadOnlyList<MonitorInfo> monitors)
    {
        var name = FindMonitor(null, monitors).Name;
        return new OverlaySettings(
            new OverlayPlacement(name, 0, OverlayPosition.LockdownBar, 0, 0, 400, 120, 100, 80),
            new OverlayPlacement(name, 0, OverlayPosition.Middle, 0, 0, 900, 700, 100, 85));
    }

    /// <summary>Saved overlays, else the migrated PinVol box, else defaults.</summary>
    public static OverlaySettings Resolve(CabSoundData data, IReadOnlyList<MonitorInfo> monitors) =>
        data.Overlays ?? (data.Display is not null ? FromPinVolBox(data.Display, monitors) : Defaults(monitors));

    // Screen direction of the player's "up" (towards the backbox) for a rotation.
    static (int X, int Y) Up(int rotation) => rotation switch
    {
        90 => (1, 0),
        180 => (0, 1),
        270 => (-1, 0),
        _ => (0, -1),
    };
}
