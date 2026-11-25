using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class ChannelCheckAdviceTests
{
    private const string Microsoft = "Microsoft 10.0.26100.9974";
    private const string CMedia = "C-Media Inc. 10.0.8.2167";

    [Fact]
    public void SameDriverSameCard_NothingToSay()
    {
        Assert.Null(ChannelCheckAdvice.For(CMedia, CMedia, cardChanged: false));
    }

    [Fact]
    public void FirstTimeTheDriverIsSeen_NothingToSay()
    {
        Assert.Null(ChannelCheckAdvice.For(null, CMedia, cardChanged: false));
    }

    [Fact]
    public void DriverCouldNotBeRead_NothingToSay()
    {
        Assert.Null(ChannelCheckAdvice.For(CMedia, null, cardChanged: false));
    }

    [Fact]
    public void DriverChanged_NamesBothAndAsksForTheSpeakerCheck()
    {
        var advice = ChannelCheckAdvice.For(Microsoft, CMedia, cardChanged: false);

        Assert.NotNull(advice);
        Assert.Contains(Microsoft, advice);
        Assert.Contains(CMedia, advice);
        Assert.Contains("Check the speakers", advice);
        Assert.DoesNotContain("Probe", advice);
        Assert.Contains("jacks", advice);
    }

    [Fact]
    public void CardChanged_AsksForTheSpeakerCheck_EvenWithoutAKnownDriver()
    {
        var advice = ChannelCheckAdvice.For(null, null, cardChanged: true);

        Assert.NotNull(advice);
        Assert.Contains("Check the speakers", advice);
        Assert.DoesNotContain("Probe", advice);
    }

    [Fact]
    public void CardAndDriverChanged_SaysTheDriverChange()
    {
        Assert.Contains(CMedia, ChannelCheckAdvice.For(Microsoft, CMedia, cardChanged: true));
    }

    [Fact]
    public void DeviceDriver_IsSavedAndLoaded_AndMissingInOldFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cabsound-{Guid.NewGuid():N}.json");
        try
        {
            Assert.Null(CabSoundData.Defaults().DeviceDriver);
            CabSoundDataFile.Save(path, CabSoundData.Defaults() with { DeviceDriver = CMedia });

            Assert.Equal(CMedia, CabSoundDataFile.Load(path).DeviceDriver);
        }
        finally { File.Delete(path); }
    }
}
