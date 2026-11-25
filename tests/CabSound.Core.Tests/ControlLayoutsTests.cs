using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class ControlLayoutsTests
{
    private static readonly InputId VolUp = InputId.Joy(20), VolDown = InputId.Joy(21);
    private static readonly InputId BgUp = InputId.Joy(19), BgDown = InputId.Joy(16);
    private static readonly InputId FrontUp = InputId.Joy(17), FrontDown = InputId.Joy(18);

    private static List<CabAction> Press(GestureDetector d, InputId b)
    {
        var fired = new List<CabAction>(d.Down(b, 0));
        fired.AddRange(d.Up(b, 100));
        return fired;
    }

    private static List<CabAction> Hold(GestureDetector d, InputId b, long ms)
    {
        var fired = new List<CabAction>(d.Down(b, 0));
        for (long t = 0; t <= ms; t += 50) fired.AddRange(d.Tick(t));
        fired.AddRange(d.Up(b, ms + 10));
        return fired;
    }

    [Fact]
    public void PinVolLayout_KeepsEveryButtonsJob()
    {
        var d = new GestureDetector(ControlLayouts.PinVolStyle(), 2000);

        Assert.Equal(new[] { CabAction.TableUp }, Press(d, VolUp));
        Assert.Equal(new[] { CabAction.TableDown }, Press(d, VolDown));
        Assert.Equal(new[] { CabAction.BackglassUp }, Press(d, BgUp));
        Assert.Equal(new[] { CabAction.BackglassDown }, Press(d, BgDown));
        Assert.Equal(new[] { CabAction.FrontUp }, Press(d, FrontUp));
        Assert.Equal(new[] { CabAction.FrontDown }, Press(d, FrontDown));
    }

    [Fact]
    public void SimpleLayout_UsesSelectedGroupAndCycleButton()
    {
        var d = new GestureDetector(ControlLayouts.Simple(), 2000);

        Assert.Equal(new[] { CabAction.AdjustUp }, Press(d, VolUp));
        Assert.Equal(new[] { CabAction.AdjustDown }, Press(d, VolDown));
        Assert.Equal(new[] { CabAction.NextGroup }, Press(d, BgUp));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothLayouts_HoldVolUpOneSecond_OpensMenu(bool pinVolStyle)
    {
        var d = new GestureDetector(pinVolStyle ? ControlLayouts.PinVolStyle() : ControlLayouts.Simple(), 2000);

        Assert.Equal(new[] { CabAction.OpenMenu }, Hold(d, VolUp, 1000));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothLayouts_HoldVolDownTwoSeconds_TogglesNightMode(bool pinVolStyle)
    {
        var d = new GestureDetector(pinVolStyle ? ControlLayouts.PinVolStyle() : ControlLayouts.Simple(), 2000);

        Assert.Equal(new[] { CabAction.NightMode }, Hold(d, VolDown, 2000));
    }

    [Fact]
    public void HoldVolDownOneSecond_IsNotNightModeYet()
    {
        var d = new GestureDetector(ControlLayouts.PinVolStyle(), 2000);

        Assert.Equal(new[] { CabAction.TableDown }, Hold(d, VolDown, 1000));
    }

    // PinVol's keys (PinVolSettings.ini on the cabinet). PinOne sends these keys for its buttons.
    private static readonly InputId LCtrl = InputId.Key(0xA2), RCtrl = InputId.Key(0xA3);

    [Theory]
    [InlineData(0xAF, CabAction.TableUp)]       // VolumeUp
    [InlineData(0xAE, CabAction.TableDown)]     // VolumeDown
    [InlineData(0x73, CabAction.BackglassUp)]   // F4
    [InlineData(0x74, CabAction.BackglassDown)] // F5
    public void PinVolKeys_SingleKeys_ActOnPress(int vk, CabAction expected)
    {
        var d = new GestureDetector(ControlLayouts.PinVolKeys(), 2000);

        Assert.Equal([expected], d.Down(InputId.Key(vk), 0));
    }

    [Theory]
    [InlineData(0x49, CabAction.FrontUp)]       // Ctrl+I
    [InlineData(0x47, CabAction.FrontDown)]     // Ctrl+G
    [InlineData(0x73, CabAction.RearUp)]        // Ctrl+F4
    [InlineData(0x74, CabAction.RearDown)]      // Ctrl+F5
    [InlineData(0x78, CabAction.GlobalUp)]      // Ctrl+F9
    [InlineData(0x77, CabAction.GlobalDown)]    // Ctrl+F8
    [InlineData(0x0D, CabAction.NightMode)]     // Ctrl+Enter
    public void PinVolKeys_CtrlCombos_WorkWithEitherCtrl(int vk, CabAction expected)
    {
        foreach (var ctrl in new[] { LCtrl, RCtrl })
        {
            var d = new GestureDetector(ControlLayouts.PinVolKeys(), 2000);
            d.Down(ctrl, 0);

            Assert.Equal([expected], d.Down(InputId.Key(vk), 10));
        }
    }

    [Fact]
    public void CtrlVolumeUp_OpensMenu()
    {
        foreach (var ctrl in new[] { LCtrl, RCtrl })
        {
            var d = new GestureDetector(ControlLayouts.PinVolKeys(), 2000);
            d.Down(ctrl, 0);

            Assert.Equal([CabAction.OpenMenu], d.Down(InputId.Key(0xAF), 10));
        }
    }

    [Fact]
    public void VolumeUpAlone_StillTableUp()
    {
        var d = new GestureDetector(ControlLayouts.PinVolKeys(), 2000);

        Assert.Equal([CabAction.TableUp], d.Down(InputId.Key(0xAF), 0));
    }

    [Theory]
    [InlineData(0x49)] // I
    [InlineData(0x47)] // G
    [InlineData(0x78)] // F9 = Start
    [InlineData(0x77)] // F8 = Credit
    [InlineData(0x0D)] // Enter = Launch
    public void PinVolKeys_WithoutCtrl_TheseKeysDoNothing(int vk)
    {
        var d = new GestureDetector(ControlLayouts.PinVolKeys(), 2000);

        Assert.Empty(d.Down(InputId.Key(vk), 0));
    }
}
