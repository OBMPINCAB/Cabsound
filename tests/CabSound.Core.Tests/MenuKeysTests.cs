using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class MenuKeysTests
{
    private static MenuKeys Real() =>
        MenuKeys.FromVpx(ConflictChecker.FromVpxIni(File.ReadAllText(Path.Combine("TestData", "VPinballX.ini"))))!;

    [Fact]
    public void FromVpx_UsesYourFlipperMagnaAndStartKeys()
    {
        var k = Real();

        Assert.Equal(0xA0, k.LeftFlipper);   // Left Shift
        Assert.Equal(0xA1, k.RightFlipper);  // Right Shift
        Assert.Equal(0xA2, k.LeftMagna);     // Left Ctrl
        Assert.Equal(0xA3, k.RightMagna);    // Right Ctrl
        Assert.Equal(0x78, k.Start);         // F9
        Assert.Equal(0x50, k.Pause);         // P
    }

    [Theory]
    [InlineData(0xA0, MenuCommand.Up)]
    [InlineData(0xA1, MenuCommand.Down)]
    [InlineData(0xA2, MenuCommand.Decrease)]
    [InlineData(0xA3, MenuCommand.Increase)]
    [InlineData(0x78, MenuCommand.Close)]
    public void ToCommand_MapsCabinetButtons(int vk, MenuCommand expected)
    {
        Assert.Equal(expected, Real().ToCommand(vk));
    }

    [Fact]
    public void ToCommand_OtherKeys_AreNotMenuKeys()
    {
        Assert.Null(Real().ToCommand(0x0D)); // plunger
    }

    [Fact]
    public void ShouldHoldBack_OnlyMenuKeys()
    {
        var k = Real();

        Assert.True(k.ShouldHoldBack(0xA0));
        Assert.True(k.ShouldHoldBack(0x78));
        Assert.False(k.ShouldHoldBack(0x52)); // Exit (R) always passes through
    }

    [Fact]
    public void FromVpx_MissingAKey_ReturnsNull()
    {
        Assert.Null(MenuKeys.FromVpx(ConflictChecker.FromVpxIni("[Player]\r\nLFlipKey = 42\r\n")));
    }
}
