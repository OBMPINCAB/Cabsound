namespace CabSound.Core;

/// <summary>One menu step on an overlay's placement (display rows of the on-screen menu).</summary>
public static class OverlayEditor
{
    private const int NudgePx = 10, SizeMin = 50, SizeMax = 200, OpacityMin = 30, OpacityMax = 100, Step10 = 10;

    /// <summary>dir = -1 or +1. Rows that aren't display settings leave the placement as it is.</summary>
    public static OverlayPlacement Step(OverlayPlacement p, MenuRow row, int dir, IReadOnlyList<MonitorInfo> monitors) => row switch
    {
        MenuRow.Rotation => p with { Rotation = (p.Rotation + dir * 90 + 360) % 360 },
        MenuRow.Position => p with { Position = (OverlayPosition)Math.Clamp((int)p.Position + dir, 0, (int)OverlayPosition.Backbox) },
        MenuRow.NudgeAlong => p with { Along = Nudge(p.Along, dir, p, monitors) },
        MenuRow.NudgeToward => p with { Toward = Nudge(p.Toward, dir, p, monitors) },
        MenuRow.Size => p with { SizePercent = Math.Clamp(p.SizePercent + dir * Step10, SizeMin, SizeMax) },
        MenuRow.Opacity => p with { OpacityPercent = Math.Clamp(p.OpacityPercent + dir * Step10, OpacityMin, OpacityMax) },
        MenuRow.Monitor => NextMonitor(p, dir, monitors),
        _ => p,
    };

    /// <summary>One nudge step, kept within the larger side of the placement's monitor.</summary>
    private static int Nudge(int value, int dir, OverlayPlacement p, IReadOnlyList<MonitorInfo> monitors)
    {
        var m = OverlayLayout.FindMonitor(p.Monitor, monitors);
        var limit = Math.Max(m.Width, m.Height);
        return Math.Clamp(value + dir * NudgePx, -limit, limit);
    }

    private static OverlayPlacement NextMonitor(OverlayPlacement p, int dir, IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0) return p;
        var current = OverlayLayout.FindMonitor(p.Monitor, monitors);
        var index = ((IList<MonitorInfo>)monitors).IndexOf(current);
        return p with { Monitor = monitors[(index + dir + monitors.Count) % monitors.Count].Name };
    }
}
