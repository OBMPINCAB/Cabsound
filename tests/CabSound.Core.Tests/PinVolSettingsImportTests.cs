using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class PinVolSettingsImportTests
{
    [Fact]
    public void Display_ReadsPinVolOsd()
    {
        var display = PinVolSettingsImport.Display("OSDPos = 3610,50,180,2035\r\nOSDRotation = 90\r\n");

        // PinVol turns counter-clockwise, CabSound clockwise (seen upside down on the cabinet with 90)
        Assert.Equal(new DisplaySettings(3610, 50, 180, 2035, 270), display);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 270)]
    [InlineData(180, 180)]
    [InlineData(270, 90)]
    public void Display_ConvertsRotationDirection(int pinVol, int cabSound)
    {
        Assert.Equal(cabSound, PinVolSettingsImport.Display($"OSDPos = 1,2,3,4\r\nOSDRotation = {pinVol}\r\n")!.Rotation);
    }

    [Fact]
    public void Display_NoRotation_DefaultsToZero()
    {
        Assert.Equal(new DisplaySettings(1, 2, 3, 4, 0), PinVolSettingsImport.Display("OSDPos = 1,2,3,4\r\n"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("OSDPos = 1,2\r\n")]
    [InlineData("OSDPos = a,b,c,d\r\n")]
    public void Display_MissingOrBadPos_IsNull(string text)
    {
        Assert.Null(PinVolSettingsImport.Display(text));
    }
}
