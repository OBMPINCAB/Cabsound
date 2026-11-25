using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class OverlayEditorTests
{
    private static readonly OverlayPlacement Base = new(null, 0, OverlayPosition.Middle, 0, 0, 900, 700, 100, 85);
    private static readonly MonitorInfo[] None = [];

    [Fact]
    public void Rotation_Wraps()
    {
        Assert.Equal(0, OverlayEditor.Step(Base with { Rotation = 270 }, MenuRow.Rotation, +1, None).Rotation);
        Assert.Equal(270, OverlayEditor.Step(Base, MenuRow.Rotation, -1, None).Rotation);
        Assert.Equal(90, OverlayEditor.Step(Base, MenuRow.Rotation, +1, None).Rotation);
    }

    [Fact]
    public void Position_Clamps()
    {
        Assert.Equal(OverlayPosition.Backbox, OverlayEditor.Step(Base, MenuRow.Position, +1, None).Position);
        Assert.Equal(OverlayPosition.LockdownBar, OverlayEditor.Step(Base, MenuRow.Position, -1, None).Position);
        Assert.Equal(OverlayPosition.LockdownBar,
            OverlayEditor.Step(Base with { Position = OverlayPosition.LockdownBar }, MenuRow.Position, -1, None).Position);
        Assert.Equal(OverlayPosition.Backbox,
            OverlayEditor.Step(Base with { Position = OverlayPosition.Backbox }, MenuRow.Position, +1, None).Position);
    }

    [Fact]
    public void Nudges_Move10Pixels()
    {
        Assert.Equal(10, OverlayEditor.Step(Base, MenuRow.NudgeAlong, +1, None).Along);
        Assert.Equal(-10, OverlayEditor.Step(Base, MenuRow.NudgeToward, -1, None).Toward);
    }

    [Fact]
    public void Nudges_StopAtTheLargerSideOfTheMonitor()
    {
        MonitorInfo[] monitors = [new("A", 0, 0, 1920, 1080, true)];
        var atLimit = Base with { Along = 1920, Toward = -1920 };
        Assert.Equal(1920, OverlayEditor.Step(atLimit, MenuRow.NudgeAlong, +1, monitors).Along);
        Assert.Equal(-1920, OverlayEditor.Step(atLimit, MenuRow.NudgeToward, -1, monitors).Toward);
        Assert.Equal(1910, OverlayEditor.Step(atLimit, MenuRow.NudgeAlong, -1, monitors).Along);
    }

    [Fact]
    public void Size_Clamps()
    {
        Assert.Equal(200, OverlayEditor.Step(Base with { SizePercent = 200 }, MenuRow.Size, +1, None).SizePercent);
        Assert.Equal(50, OverlayEditor.Step(Base with { SizePercent = 50 }, MenuRow.Size, -1, None).SizePercent);
        Assert.Equal(110, OverlayEditor.Step(Base, MenuRow.Size, +1, None).SizePercent);
    }

    [Fact]
    public void Opacity_Clamps()
    {
        Assert.Equal(30, OverlayEditor.Step(Base with { OpacityPercent = 30 }, MenuRow.Opacity, -1, None).OpacityPercent);
        Assert.Equal(100, OverlayEditor.Step(Base with { OpacityPercent = 100 }, MenuRow.Opacity, +1, None).OpacityPercent);
        Assert.Equal(90, OverlayEditor.Step(Base with { OpacityPercent = 100 }, MenuRow.Opacity, -1, None).OpacityPercent);
    }

    [Fact]
    public void Monitor_CyclesAndWraps()
    {
        MonitorInfo[] monitors = [new("A", 0, 0, 1920, 1080, true), new("B", 1920, 0, 1920, 1080, false), new("C", 3840, 0, 1920, 1080, false)];

        Assert.Equal("B", OverlayEditor.Step(Base with { Monitor = "A" }, MenuRow.Monitor, +1, monitors).Monitor);
        Assert.Equal("A", OverlayEditor.Step(Base with { Monitor = "C" }, MenuRow.Monitor, +1, monitors).Monitor);
        Assert.Equal("C", OverlayEditor.Step(Base with { Monitor = "A" }, MenuRow.Monitor, -1, monitors).Monitor);
        Assert.Equal("B", OverlayEditor.Step(Base, MenuRow.Monitor, +1, monitors).Monitor); // null = primary (A)
    }

    [Fact]
    public void Monitor_WithNoMonitors_ChangesNothing() =>
        Assert.Equal(Base, OverlayEditor.Step(Base, MenuRow.Monitor, +1, None));

    [Fact]
    public void OtherRows_ChangeNothing() =>
        Assert.Equal(Base, OverlayEditor.Step(Base, MenuRow.Reset, +1, None));
}
