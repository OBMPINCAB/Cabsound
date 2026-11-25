using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class ButtonPressesTests
{
    private static readonly InputId F4 = InputId.Key(0x73), Ctrl = InputId.Key(0xA2);

    // Repeat: first after 400 ms, then every 100 ms
    private static ButtonPresses Presses() => new(ControlLayouts.PinVolKeys(), holdMs: 2000);

    [Fact]
    public void Key_ActsOnPress_AndRepeatsWhileHeld()
    {
        var p = Presses();

        Assert.Equal([CabAction.BackglassUp], p.Down(F4, 0));
        Assert.Empty(p.Tick(399));
        Assert.Equal([CabAction.BackglassUp], p.Tick(400));
        Assert.Equal([CabAction.BackglassUp], p.Tick(500));
        Assert.Empty(p.Up(F4, 550));
        Assert.Empty(p.Tick(1000));
    }

    [Fact]
    public void OpenMenu_DoesNotRepeat()
    {
        var p = Presses();

        p.Down(Ctrl, 0);
        Assert.Equal([CabAction.OpenMenu], p.Down(InputId.Key(0xAF), 10));
        Assert.Empty(p.Tick(1000));
    }

    [Fact]
    public void KeyboardAutoRepeat_DoesNotFireExtraActions()
    {
        var p = Presses();

        p.Down(F4, 0);
        Assert.Empty(p.Down(F4, 30)); // Windows repeats key-down while held
        Assert.Empty(p.Down(F4, 60));
    }

    [Fact]
    public void CtrlCombo_RepeatsWhileKeyHeld()
    {
        var p = Presses();

        p.Down(Ctrl, 0);
        Assert.Equal([CabAction.RearUp], p.Down(F4, 10));
        Assert.Equal([CabAction.RearUp], p.Tick(410));
        Assert.Empty(p.Up(F4, 450));
        Assert.Empty(p.Tick(1000));
    }

    [Fact]
    public void HoldBinding_RepeatsAfterItFires()
    {
        var a = InputId.Joy(1);
        var p = new ButtonPresses([new Binding(CabAction.AdjustUp, [a]), new Binding(CabAction.RearUp, [a], Hold: true, HoldMs: 1000)], holdMs: 2000);

        Assert.Empty(p.Down(a, 0));
        Assert.Equal([CabAction.RearUp], p.Tick(1000));
        Assert.Equal([CabAction.RearUp], p.Tick(1400));
        Assert.Empty(p.Up(a, 1450)); // no extra single step on release
        Assert.Empty(p.Tick(3000));
    }
}
