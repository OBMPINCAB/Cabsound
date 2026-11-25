using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class StartupPlanTests
{
    private const string PinVolLink = "PinVol.exe - Shortcut.lnk";

    [Fact]
    public void Enable_ParksPinVolAndAddsCabSound()
    {
        var steps = StartupPlan.Enable([PinVolLink, "PinUpMenu.exe - Shortcut.lnk"], []);

        Assert.Equal([StartupStep.ParkPinVol, StartupStep.AddCabSound], steps);
    }

    [Fact]
    public void Enable_AlreadyDone_NoSteps()
    {
        Assert.Empty(StartupPlan.Enable([StartupPlan.CabSoundLink], [PinVolLink]));
    }

    [Fact]
    public void Disable_RestoresPinVolAndRemovesCabSound()
    {
        var steps = StartupPlan.Disable([StartupPlan.CabSoundLink], [PinVolLink]);

        Assert.Equal([StartupStep.RemoveCabSound, StartupStep.RestorePinVol], steps);
    }

    [Fact]
    public void PinVolShortcut_MatchedIgnoringCase()
    {
        Assert.Equal([StartupStep.ParkPinVol, StartupStep.AddCabSound], StartupPlan.Enable(["pinvol.lnk"], []));
    }

    [Fact]
    public void IsEnabled_WhenCabSoundShortcutPresent()
    {
        Assert.True(StartupPlan.IsEnabled([StartupPlan.CabSoundLink]));
        Assert.False(StartupPlan.IsEnabled([PinVolLink]));
    }
}
