using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class OverlayLayoutTests
{
    static string Dev(int n) => new string((char)92, 2) + "." + (char)92 + "DISPLAY" + n;
    static readonly MonitorInfo Play = new(Dev(3), 0, 0, 3840, 2160, true);
    static readonly MonitorInfo Back = new(Dev(1), 3840, 0, 2560, 1440, false);
    static readonly MonitorInfo[] Both = [Back, Play];

    static OverlayPlacement P(OverlayPosition pos, int rotation = 0, int along = 0, int toward = 0,
        int size = 100, string? monitor = null) =>
        new(monitor, rotation, pos, along, toward, 400, 120, size, 80);

    [Fact]
    public void Middle_Rotation0_Centred() =>
        Assert.Equal(new OverlayLayout.PixelRect(1720, 1020, 400, 120),
            OverlayLayout.Place(P(OverlayPosition.Middle), Both));

    [Theory]
    [InlineData(0, 1720, 2040, 400, 120)]
    [InlineData(90, 0, 880, 120, 400)]
    [InlineData(180, 1720, 0, 400, 120)]
    [InlineData(270, 3720, 880, 120, 400)]
    public void LockdownBar_PerRotation(int rot, int x, int y, int w, int h) =>
        Assert.Equal(new OverlayLayout.PixelRect(x, y, w, h),
            OverlayLayout.Place(P(OverlayPosition.LockdownBar, rot), Both));

    [Fact]
    public void NoMonitors_UsesStandIn() =>
        Assert.Equal(new MonitorInfo("", 0, 0, 1920, 1080, true), OverlayLayout.FindMonitor(null, []));

    [Fact]
    public void Backbox_Rotation270_LeftEdge() =>
        Assert.Equal(new OverlayLayout.PixelRect(0, 880, 120, 400),
            OverlayLayout.Place(P(OverlayPosition.Backbox, 270), Both));

    [Fact]
    public void Toward_And_Along_Rotation270()
    {
        var baseRect = OverlayLayout.Place(P(OverlayPosition.Middle, 270), Both);
        var moved = OverlayLayout.Place(P(OverlayPosition.Middle, 270, along: 12, toward: 50), Both);
        Assert.Equal(baseRect.X - 50, moved.X);
        Assert.Equal(baseRect.Y - 12, moved.Y);
    }

    [Fact]
    public void Size150_ScalesBothSides()
    {
        var r = OverlayLayout.Place(P(OverlayPosition.Middle, size: 150), Both);
        Assert.Equal(600, r.Width);
        Assert.Equal(180, r.Height);
    }

    [Fact]
    public void Place_ClampsInsideMonitor()
    {
        var r = OverlayLayout.Place(P(OverlayPosition.Middle, toward: 99999), Both);
        Assert.True(r.X >= 0 && r.Y >= 0 && r.X + r.Width <= 3840 && r.Y + r.Height <= 2160);
    }

    [Fact]
    public void Place_BoxLargerThanMonitor_PinnedToTopLeft()
    {
        var p = new OverlayPlacement(null, 0, OverlayPosition.Middle, 0, 0, 5000, 3000);
        var r = OverlayLayout.Place(p, Both);
        Assert.Equal(0, r.X);
        Assert.Equal(0, r.Y);
    }

    [Fact]
    public void OnSecondMonitor_UsesItsBounds() =>
        Assert.Equal(new OverlayLayout.PixelRect(3840 + 1080, 660, 400, 120),
            OverlayLayout.Place(P(OverlayPosition.Middle, monitor: Back.Name), Both));

    [Fact]
    public void UnknownMonitor_UsesPrimary() =>
        Assert.Equal(OverlayLayout.Place(P(OverlayPosition.Middle), Both),
            OverlayLayout.Place(P(OverlayPosition.Middle, monitor: Dev(9)), Both));

    [Fact]
    public void FromPinVolBox_TodaysBox_PlacesExactly()
    {
        var s = OverlayLayout.FromPinVolBox(new DisplaySettings(3610, 50, 180, 2035, 270), Both);
        Assert.Equal(new OverlayPlacement(Play.Name, 270, OverlayPosition.LockdownBar, 12, 50, 2035, 180, 100, 80), s.Popup);
        Assert.Equal(new OverlayLayout.PixelRect(3610, 50, 180, 2035), OverlayLayout.Place(s.Popup, Both));
        Assert.Equal(new OverlayPlacement(Play.Name, 270, OverlayPosition.Middle, 0, 0, 900, 700, 100, 85), s.Menu);
    }

    [Fact]
    public void FromPinVolBox_CentreOnNoMonitor_UsesPrimary()
    {
        var s = OverlayLayout.FromPinVolBox(new DisplaySettings(-5000, 0, 400, 120, 0), Both);
        Assert.Equal(Play.Name, s.Popup.Monitor);
    }

    [Fact]
    public void Defaults_UsePrimaryAndSpecValues()
    {
        var s = OverlayLayout.Defaults(Both);
        Assert.Equal(new OverlayPlacement(Play.Name, 0, OverlayPosition.LockdownBar, 0, 0, 400, 120, 100, 80), s.Popup);
        Assert.Equal(new OverlayPlacement(Play.Name, 0, OverlayPosition.Middle, 0, 0, 900, 700, 100, 85), s.Menu);
    }

    [Fact]
    public void Resolve_PrefersOverlays_ThenDisplay_ThenDefaults()
    {
        var saved = new OverlaySettings(P(OverlayPosition.Backbox), P(OverlayPosition.Middle));
        var box = new DisplaySettings(3610, 50, 180, 2035, 270);
        var data = CabSoundData.Defaults();

        Assert.Equal(saved, OverlayLayout.Resolve(data with { Overlays = saved, Display = box }, Both));
        Assert.Equal(OverlayLayout.FromPinVolBox(box, Both), OverlayLayout.Resolve(data with { Display = box }, Both));
        Assert.Equal(OverlayLayout.Defaults(Both), OverlayLayout.Resolve(data, Both));
    }
}
