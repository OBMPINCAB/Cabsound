using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class ToolWindowLayoutTests
{
    static readonly MonitorInfo Desk = new("desk", 0, 0, 1920, 1080, true);
    static readonly MonitorInfo Playfield = new("pf", 1920, 0, 3840, 2160, false);

    [Theory]
    [InlineData(0, 90)]
    [InlineData(90, 180)]
    [InlineData(180, 270)]
    [InlineData(270, 0)]
    [InlineData(45, 90)]
    public void NextRotation_StepsBy90(int from, int to) => Assert.Equal(to, ToolWindowLayout.NextRotation(from));

    [Fact]
    public void Rotation_NeverSetOrOdd_IsUpright()
    {
        Assert.Equal(0, ToolWindowLayout.Rotation(null));
        Assert.Equal(0, ToolWindowLayout.Rotation(new ToolWindowPlace(45, 0, 0)));
        Assert.Equal(270, ToolWindowLayout.Rotation(new ToolWindowPlace(270, 0, 0)));
    }

    [Fact]
    public void TopLeft_NeverSet_CentresOnThePrimary() =>
        Assert.Equal((640, 340), ToolWindowLayout.TopLeft(null, 640, 400, [Playfield, Desk]));

    [Fact]
    public void TopLeft_SavedOnAScreen_IsKept() =>
        Assert.Equal((2500, 300), ToolWindowLayout.TopLeft(new ToolWindowPlace(90, 2500, 300), 400, 640, [Desk, Playfield]));

    [Fact]
    public void TopLeft_SavedScreenGone_CentresOnThePrimary() =>
        Assert.Equal((760, 220), ToolWindowLayout.TopLeft(new ToolWindowPlace(90, 2500, 300), 400, 640, [Desk]));

    [Fact]
    public void TopLeft_NoPrimary_UsesTheFirst() =>
        Assert.Equal((1920 + 1600, 880), ToolWindowLayout.TopLeft(null, 640, 400, [Playfield]));

    [Fact]
    public void TopLeft_NoMonitors_IsZero() =>
        Assert.Equal((0, 0), ToolWindowLayout.TopLeft(null, 640, 400, []));

    // The bar is the content's top edge: at 0° the window's top, 90° its right, 180° its bottom, 270° its left.
    [Theory]
    [InlineData(0, 100, 200)]     // grows down / right from a fixed top-left: nothing to do
    [InlineData(90, 0, 200)]      // bar on the right edge: keep the right edge where it was
    [InlineData(180, 100, 100)]   // bar on the bottom edge: keep the bottom edge where it was
    [InlineData(270, 100, 200)]   // bar on the left edge: the top-left already keeps it
    public void AfterResize_KeepsTheBarWhereItWas(int rotation, int x, int y) =>
        Assert.Equal((x, y), ToolWindowLayout.AfterResize(rotation, 100, 200, 400, 640, 500, 740));

    [Fact]
    public void AfterResize_Shrinking_MovesTheOtherWay() =>
        Assert.Equal((200, 200), ToolWindowLayout.AfterResize(90, 100, 200, 500, 640, 400, 640));

    [Fact]
    public void AfterTurn_KeepsTheCentre() =>
        Assert.Equal((220, 80), ToolWindowLayout.AfterTurn(100, 200, 640, 400));   // centre (420, 400)
}
