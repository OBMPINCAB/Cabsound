using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class SoundSettingsTests
{
    [Fact]
    public void Step_WalksTheListLeastToMostBass_AndStopsAtTheEnds()
    {
        Assert.Equal(150, LowCutChoices.Step(null, +1));
        Assert.Equal(120, LowCutChoices.Step(150, +1));
        Assert.Equal(0, LowCutChoices.Step(0, +1));
        Assert.Null(LowCutChoices.Step(150, -1));
        Assert.Null(LowCutChoices.Step(null, -1));
        Assert.Equal(50, LowCutChoices.Step(55, +1)); // unlisted value moves toward more bass (50 Hz)
        Assert.Equal(60, LowCutChoices.Step(55, -1)); // unlisted value moves toward less bass (60 Hz)
    }

    [Theory]
    [InlineData(null, "Cabinet's cut")]
    [InlineData(0, "Off")]
    [InlineData(90, "90 Hz")]
    public void Label_IsPlainWords(int? value, string expected) => Assert.Equal(expected, LowCutChoices.Label(value));

    [Fact]
    public void IsRisky_OnlyExcitersOffOrUnder60()
    {
        Assert.True(LowCutChoices.IsRisky(LowCutGroup.Exciters, 0));
        Assert.True(LowCutChoices.IsRisky(LowCutGroup.Exciters, 40));
        Assert.False(LowCutChoices.IsRisky(LowCutGroup.Exciters, 60));
        Assert.False(LowCutChoices.IsRisky(LowCutGroup.Exciters, null));
        Assert.False(LowCutChoices.IsRisky(LowCutGroup.Backbox, 0));
    }

    [Fact]
    public void Defaults_AreOriginal_DipOn_NotSwapped()
    {
        Assert.Equal(new SoundSettings(LowCutMode.Original, true, false, false), new SoundSettings());
    }
}
