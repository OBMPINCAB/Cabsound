using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class EqApoSetupTests
{
    [Fact]
    public void NoStep_WhenEqualizerApoIsActive()
    {
        Assert.Null(EqApoSetup.StepFor(EqApoStatus.Active, "Card"));
    }

    [Fact]
    public void NotInstalled_OpensTheDownloadPage_AndNamesTheCardToTick()
    {
        var step = EqApoSetup.StepFor(EqApoStatus.NotInstalled, "Speakers (USB Sound Device)")!;
        Assert.Equal(EqApoSetupAction.OpenDownloadPage, step.Action);
        Assert.Contains("Speakers (USB Sound Device)", step.Text);
        Assert.StartsWith("https://", EqApoSetup.DownloadPage);
    }

    [Fact]
    public void NotOnDevice_OpensTheConfigurator_AndNamesTheCard()
    {
        var step = EqApoSetup.StepFor(EqApoStatus.NotOnDevice, "Speakers (USB Sound Device)")!;
        Assert.Equal(EqApoSetupAction.OpenConfigurator, step.Action);
        Assert.Contains("Speakers (USB Sound Device)", step.Text);
    }

    [Fact]
    public void EnhancementsOff_IsFixedByCabSound()
    {
        var step = EqApoSetup.StepFor(EqApoStatus.EnhancementsOff, "Card")!;
        Assert.Equal(EqApoSetupAction.TurnOnEnhancements, step.Action);
    }

    [Fact]
    public void EveryStep_HasTitleTextAndButton()
    {
        foreach (var status in new[] { EqApoStatus.NotInstalled, EqApoStatus.NotOnDevice, EqApoStatus.EnhancementsOff })
        {
            var step = EqApoSetup.StepFor(status, "Card")!;
            Assert.False(string.IsNullOrWhiteSpace(step.Title));
            Assert.False(string.IsNullOrWhiteSpace(step.Text));
            Assert.False(string.IsNullOrWhiteSpace(step.Button));
        }
    }

    [Fact]
    public void HelperArgs_RoundTrip()
    {
        var id = "{0.0.0.00000000}.{11111111-2222-3333-4444-555555555555}";
        Assert.Equal(id, EqApoSetup.DeviceToFix(EqApoSetup.FixEnhancementsArgs(id)));
    }

    [Theory]
    [InlineData()]
    [InlineData("--other", "x")]
    [InlineData("--fix-enhancements")]
    public void DeviceToFix_IsNull_ForOtherArgs(params string[] args)
    {
        Assert.Null(EqApoSetup.DeviceToFix(args));
    }

    [Fact]
    public void DeviceToFix_RejectsAnIdThatIsNotAnEndpoint()
    {
        Assert.Null(EqApoSetup.DeviceToFix(["--fix-enhancements", @"..\..\Software"]));
    }

    [Fact]
    public void EndpointGuid_IsTheLastPartOfTheDeviceId()
    {
        Assert.Equal("{11111111-2222-3333-4444-555555555555}",
            EqApoSetup.EndpointGuid("{0.0.0.00000000}.{11111111-2222-3333-4444-555555555555}"));
    }

    [Fact]
    public void EnhancementsOnValue_ClearsWindowsBypassSwitch()
    {
        var fx = new Dictionary<string, object?>
        {
            ["{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},2"] = "{EC1CC9CE-FAED-4822-828A-82A81A6F018F}",
            [EqApoHealth.EnhancementsOffValue] = 0,
        };
        Assert.Equal(EqApoStatus.Active, EqApoHealth.Status(true, fx));
    }
}
