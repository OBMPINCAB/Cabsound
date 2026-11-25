using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class ConflictCheckerTests
{
    private static List<ReservedInput> Vpx() =>
        ConflictChecker.FromVpxIni(File.ReadAllText(Path.Combine("TestData", "VPinballX.ini")));

    [Theory]
    [InlineData(0xA0, "LFlipKey")]      // Left Shift
    [InlineData(0xA1, "RFlipKey")]      // Right Shift
    [InlineData(0xA2, "LMagnaSave")]    // Left Ctrl
    [InlineData(0xA3, "RMagnaSave")]    // Right Ctrl
    [InlineData(0x78, "StartGameKey")]  // F9
    [InlineData(0x51, "ExitGameKey")]   // Q
    [InlineData(0x0D, "PlungerKey")]    // Enter
    public void Vpx_RealIni_ReservesPlayKeysAsVirtualKeys(int vk, string name)
    {
        Assert.Contains(Vpx(), r => r.Input == InputId.Key(vk) && r.Owner == "VPX" && r.Name == name);
    }

    [Fact]
    public void Vpx_RealIni_ReservesJoystickButtonsOnlyWhenSet()
    {
        var joys = Vpx().Where(r => r.Input.Kind == InputKind.Joystick).ToList();

        var mechTilt = Assert.Single(joys);
        Assert.Equal(InputId.Joy(9), mechTilt.Input);
        Assert.Equal("JoyMechTiltKey", mechTilt.Name);
    }

    [Fact]
    public void Vpx_IgnoresKeysOutsidePlayerSection()
    {
        var r = ConflictChecker.FromVpxIni("[Other]\r\nLFlipKey = 42\r\n[Player]\r\nRFlipKey = 54\r\n");

        Assert.Equal(new[] { "RFlipKey" }, r.Select(x => x.Name));
    }

    // Popper stores joystick buttons 0-based; PinOne/Windows report them 1-based.
    [Fact]
    public void Popper_ReservesActiveKeysAndJoysticks()
    {
        var rows = new[]
        {
            new PopperControl("Game Start", "120", "-1", true),
            new PopperControl("Exit Emulators", "82", "7", true),
            new PopperControl("Game Prior", "0", "3", true),
            new PopperControl("List Next", null, "-1", false),
        };

        var r = ConflictChecker.FromPopper(rows);

        Assert.Contains(r, x => x.Input == InputId.Key(0x78) && x.Name == "Game Start");
        Assert.Contains(r, x => x.Input == InputId.Key(82) && x.Name == "Exit Emulators");
        Assert.Contains(r, x => x.Input == InputId.Joy(8) && x.Name == "Exit Emulators");
        Assert.Contains(r, x => x.Input == InputId.Joy(4) && x.Name == "Game Prior");
        Assert.Equal(4, r.Count);
    }

    [Fact]
    public void Find_ReturnsEveryClashForABinding()
    {
        var reserved = Vpx();
        var binding = new Binding(CabAction.NightMode, new[] { InputId.Key(0xA2), InputId.Key(0x0D) });

        var clashes = ConflictChecker.Find(binding, reserved);

        Assert.Equal(new[] { "LMagnaSave", "PlungerKey" }, clashes.Select(c => c.Name).OrderBy(n => n));
    }

    [Fact]
    public void Popper_JoystickZero_IsButtonOne()
    {
        var r = ConflictChecker.FromPopper(new[] { new PopperControl("X", null, "0", true) });

        Assert.Equal(InputId.Joy(1), Assert.Single(r).Input);
    }

    [Fact]
    public void Find_ChecksTheKeyTwinOfAPinOneButton()
    {
        // PinOne left flipper sends Left Shift + joystick button 4 together
        var twins = new ButtonTwins();
        twins.Pair(InputId.Joy(4), InputId.Key(0xA0));
        var binding = new Binding(CabAction.AdjustUp, new[] { InputId.Joy(4) });

        var clash = Assert.Single(ConflictChecker.Find(binding, Vpx(), twins));
        Assert.Equal("LFlipKey", clash.Name);
    }

    [Fact]
    public void Find_SpareJoystickButton_HasNoClash()
    {
        var binding = new Binding(CabAction.AdjustUp, new[] { InputId.Joy(20) });

        Assert.Empty(ConflictChecker.Find(binding, Vpx()));
    }
}
