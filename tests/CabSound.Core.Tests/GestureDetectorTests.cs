using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class GestureDetectorTests
{
    private static readonly InputId A = InputId.Joy(20);
    private static readonly InputId B = InputId.Joy(21);
    private static readonly InputId Other = InputId.Joy(5);

    // Suggested default: A = up, B = down, A+B = next group, hold B 2 s = night mode
    private static GestureDetector Default() => new(new[]
    {
        new Binding(CabAction.AdjustUp, new[] { A }),
        new Binding(CabAction.AdjustDown, new[] { B }),
        new Binding(CabAction.NextGroup, new[] { A, B }),
        new Binding(CabAction.NightMode, new[] { B }, Hold: true),
    }, holdMs: 2000);

    [Fact]
    public void SinglePress_FiresOnRelease_WhenButtonAlsoUsedInChord()
    {
        var d = Default();

        Assert.Empty(d.Down(A, 0));
        Assert.Equal(new[] { CabAction.AdjustUp }, d.Up(A, 100));
    }

    [Fact]
    public void SinglePress_FiresOnPress_WhenButtonHasNoChordOrHold()
    {
        var d = new GestureDetector(new[] { new Binding(CabAction.Mute, new[] { Other }) }, 2000);

        Assert.Equal(new[] { CabAction.Mute }, d.Down(Other, 0));
        Assert.Empty(d.Up(Other, 50));
    }

    [Fact]
    public void Chord_FiresOnce_AndSuppressesSingles()
    {
        var d = Default();

        d.Down(A, 0);
        Assert.Equal(new[] { CabAction.NextGroup }, d.Down(B, 50));
        Assert.Empty(d.Up(A, 120));
        Assert.Empty(d.Up(B, 130));
    }

    [Fact]
    public void Hold_FiresAfterHoldTime_AndSuppressesSingle()
    {
        var d = Default();

        d.Down(B, 0);
        Assert.Empty(d.Tick(1999));
        Assert.Equal(new[] { CabAction.NightMode }, d.Tick(2000));
        Assert.Empty(d.Tick(2500));
        Assert.Empty(d.Up(B, 2600));
    }

    [Fact]
    public void ShortPressOnHoldButton_GivesSingleAction()
    {
        var d = Default();

        d.Down(B, 0);
        d.Tick(500);
        Assert.Equal(new[] { CabAction.AdjustDown }, d.Up(B, 600));
    }

    [Fact]
    public void UnboundInput_IsIgnored()
    {
        var d = Default();

        Assert.Empty(d.Down(Other, 0));
        Assert.Empty(d.Up(Other, 10));
    }

    [Fact]
    public void KeyAndJoystickWithSameNumber_AreDifferentInputs()
    {
        var d = new GestureDetector(new[] { new Binding(CabAction.Mute, new[] { InputId.Key(20) }) }, 2000);

        Assert.Empty(d.Down(InputId.Joy(20), 0));
        Assert.Equal(new[] { CabAction.Mute }, d.Down(InputId.Key(20), 10));
    }

    // PinVol-style combo: modifier held, then key (Ctrl+F4). The key alone still acts on press.
    private static readonly InputId Ctrl = InputId.Key(0xA2), F4 = InputId.Key(0x73);

    private static GestureDetector WithCombo() => new(new[]
    {
        new Binding(CabAction.BackglassUp, new[] { F4 }),
        new Binding(CabAction.RearUp, new[] { Ctrl, F4 }, Combo: true),
    }, holdMs: 2000);

    [Fact]
    public void Combo_KeyAlone_ActsOnPress()
    {
        var d = WithCombo();

        Assert.Equal([CabAction.BackglassUp], d.Down(F4, 0));
        Assert.Empty(d.Up(F4, 100));
    }

    [Fact]
    public void Combo_KeyWhileModifierHeld_FiresCombo_EveryPress()
    {
        var d = WithCombo();

        Assert.Empty(d.Down(Ctrl, 0));
        Assert.Equal([CabAction.RearUp], d.Down(F4, 10));
        Assert.Empty(d.Up(F4, 100));
        Assert.Equal([CabAction.RearUp], d.Down(F4, 200)); // modifier still held
        Assert.Empty(d.Up(F4, 300));
        Assert.Empty(d.Up(Ctrl, 400));
    }

    [Fact]
    public void Combo_ModifierAfterKey_IsNotACombo()
    {
        var d = WithCombo();

        Assert.Equal([CabAction.BackglassUp], d.Down(F4, 0));
        Assert.Empty(d.Down(Ctrl, 10));
    }
}
