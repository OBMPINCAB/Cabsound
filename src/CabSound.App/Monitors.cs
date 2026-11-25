using System.Collections.Generic;
using System.Linq;
using CabSound.Core;
using Forms = System.Windows.Forms;

namespace CabSound.App;

/// <summary>The connected monitors, in the same pixels the pop-up is placed in.</summary>
public static class Monitors
{
    public static IReadOnlyList<MonitorInfo> All() => Forms.Screen.AllScreens
        .Select(s => new MonitorInfo(s.DeviceName, s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height, s.Primary))
        .ToList();
}
