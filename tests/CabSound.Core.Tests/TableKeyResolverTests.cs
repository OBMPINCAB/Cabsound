using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class TableKeyResolverTests
{
    [Fact]
    public void Vpx_PlayFromPopper_GivesVpKeyMatchingPinVol()
    {
        var key = TableKeyResolver.FromProcess(
            "VPinballX64.exe",
            "\"C:\\vPinball\\VisualPinball\\VPinballX64.exe\" -minimized -play \"C:\\vPinball\\VisualPinball\\Tables\\Attack from Mars (Bally 1995) LW 2.02.vpx\"");

        Assert.Equal("VP.Attack from Mars (Bally 1995) LW 2.02", key);
    }

    [Fact]
    public void Vpx_GL32Build_AlsoWorks()
    {
        var key = TableKeyResolver.FromProcess(
            "VPinballX_GL32.exe",
            "VPinballX_GL32.exe -play \"C:\\Tables\\Batman66_1.1.0.vpx\"");

        Assert.Equal("VP.Batman66_1.1.0", key);
    }

    [Fact]
    public void Vpx_SwitchCaseAndEditMode_AreHandled()
    {
        var key = TableKeyResolver.FromProcess(
            "vpinballx64.exe",
            "vpinballx64.exe -Edit \"C:\\Tables\\Fish Tales (Williams 1992) VPW 1.1.vpx\"");

        Assert.Equal("VP.Fish Tales (Williams 1992) VPW 1.1", key);
    }

    [Fact]
    public void Vpx_NoTableArgument_GivesAppKey()
    {
        Assert.Equal("Visual Pinball", TableKeyResolver.FromProcess("VPinballX64.exe", "VPinballX64.exe"));
    }

    [Fact]
    public void FuturePinball_GivesFpKey()
    {
        var key = TableKeyResolver.FromProcess(
            "Future Pinball.exe",
            "\"C:\\vPinball\\FuturePinball\\Future Pinball.exe\" /open \"C:\\vPinball\\FuturePinball\\Tables\\Masters of the Universe - Collector's Edition (PinEvent V2, FizX - 1.4).fpt\" /play /exit");

        Assert.Equal("FP.Masters of the Universe - Collector's Edition (PinEvent V2, FizX - 1.4)", key);
    }

    [Fact]
    public void Popper_GivesPopperKey()
    {
        Assert.Equal("PinUP Popper", TableKeyResolver.FromProcess("PinUpMenu.exe", "PinUpMenu.exe"));
    }

    [Fact]
    public void UnknownProgram_GivesNull()
    {
        Assert.Null(TableKeyResolver.FromProcess("notepad.exe", "notepad.exe"));
    }
}
