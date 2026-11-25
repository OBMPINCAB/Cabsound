using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

/// <summary>The rattle rule: Off or under 60 Hz on exciters is risky; backbox never is.</summary>
public class LowCutChoicesTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(40, true)]
    [InlineData(50, true)]
    [InlineData(60, false)]
    [InlineData(90, false)]
    [InlineData(120, false)]
    [InlineData(150, false)]
    [InlineData(null, false)]
    public void IsRisky_ExcitersUnder60OrOff(int? cut, bool risky)
    {
        Assert.Equal(risky, LowCutChoices.IsRisky(LowCutGroup.Exciters, cut));
        Assert.Equal(risky, LowCutChoices.IsRisky(LowCutGroup.RearExciters, cut));
        Assert.False(LowCutChoices.IsRisky(LowCutGroup.Backbox, cut));
    }

    [Fact]
    public void IsRisky_SoundOverload_UsesTablesOwnCutElseCabinets()
    {
        var exciter40 = new SoundSettings(ExciterCut: 40);
        var exciter90 = new SoundSettings(ExciterCut: 90);

        Assert.True(LowCutChoices.IsRisky(exciter40, LowCutGroup.Exciters, null));   // cabinet's 40 Hz
        Assert.False(LowCutChoices.IsRisky(exciter40, LowCutGroup.Exciters, 90));    // table's own cut wins
        Assert.True(LowCutChoices.IsRisky(exciter90, LowCutGroup.Exciters, 40));     // table's own cut is risky
        Assert.False(LowCutChoices.IsRisky(exciter90, LowCutGroup.Exciters, null));  // cabinet's 90 Hz
        Assert.False(LowCutChoices.IsRisky(new SoundSettings(BackboxCut: 0), LowCutGroup.Backbox, null));
    }

    [Fact]
    public void All_MatchesCabinetOrder_LeastToMostBass_Including50And150()
    {
        Assert.Equal([null, 150, 120, 90, 60, 50, 40, 0], LowCutChoices.All);
    }

    [Fact]
    public void Step_IncreaseMovesTowardMoreBass_DecreaseMovesBackTowardCabinetsCut()
    {
        Assert.Null(LowCutChoices.Step(null, -1));
        Assert.Equal(150, LowCutChoices.Step(null, +1));
        Assert.Equal(120, LowCutChoices.Step(150, +1));
        Assert.Equal(90, LowCutChoices.Step(120, +1));
        Assert.Equal(60, LowCutChoices.Step(90, +1));
        Assert.Equal(50, LowCutChoices.Step(60, +1));
        Assert.Equal(40, LowCutChoices.Step(50, +1));
        Assert.Equal(0, LowCutChoices.Step(40, +1));
        Assert.Equal(0, LowCutChoices.Step(0, +1));      // stops at Off (most bass)
        Assert.Equal(40, LowCutChoices.Step(0, -1));
        Assert.Null(LowCutChoices.Step(150, -1));
    }
}
