using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

/// <summary>
/// Cabinet-wide and per-table low cuts. On this cabinet the amp builds the sub from the backbox channels
/// and the shaker from the exciter channels, so these two cuts are the sub and shaker controls.
/// </summary>
public class CabinetLowCutTests
{
    private static readonly SoundSettings Original = new();
    private static readonly SoundSettings Gentle = new(LowCutMode.Gentle);

    [Fact]
    public void Cabinet_OwnCutElseModeDefault()
    {
        Assert.Null(LowCutChoices.Cabinet(Original, LowCutGroup.Backbox)); // null = the two original 90 Hz cuts
        Assert.Null(LowCutChoices.Cabinet(Original, LowCutGroup.Exciters));
        Assert.Null(LowCutChoices.Cabinet(Original, LowCutGroup.RearExciters));
        Assert.Equal(40, LowCutChoices.Cabinet(Gentle, LowCutGroup.Backbox));
        Assert.Equal(60, LowCutChoices.Cabinet(Gentle, LowCutGroup.Exciters));
        Assert.Equal(60, LowCutChoices.Cabinet(Gentle, LowCutGroup.RearExciters));
        Assert.Equal(0, LowCutChoices.Cabinet(new SoundSettings(ExciterCut: 0), LowCutGroup.Exciters));
        Assert.Equal(0, LowCutChoices.Cabinet(new SoundSettings(ExciterCut: 0), LowCutGroup.RearExciters));
        Assert.Equal(90, LowCutChoices.Cabinet(new SoundSettings(ExciterCut: 40, RearExciterCut: 90), LowCutGroup.RearExciters));
        Assert.Equal(150, LowCutChoices.Cabinet(new SoundSettings(LowCutMode.Gentle, BackboxCut: 150), LowCutGroup.Backbox));
    }

    [Fact]
    public void WithCabinetCut_PreservesOtherGroupWhenSwitchingFromGentle()
    {
        var updated = Gentle.WithCabinetCut(LowCutGroup.Backbox, null);

        Assert.Null(LowCutChoices.Cabinet(updated, LowCutGroup.Backbox));
        Assert.Equal(60, LowCutChoices.Cabinet(updated, LowCutGroup.Exciters));
        Assert.Equal(60, LowCutChoices.Cabinet(updated, LowCutGroup.RearExciters));

        var splitRear = updated.WithCabinetCut(LowCutGroup.RearExciters, 90);
        Assert.Equal(60, LowCutChoices.Cabinet(splitRear, LowCutGroup.Exciters));
        Assert.Equal(90, LowCutChoices.Cabinet(splitRear, LowCutGroup.RearExciters));
    }

    [Fact]
    public void LowCuts_NullIsTheTwoOriginalCuts_ZeroIsNone()
    {
        Assert.Equal([90, 90], EqApoFile.LowCuts(null));
        Assert.Empty(EqApoFile.LowCuts(0));
        Assert.Equal([150], EqApoFile.LowCuts(150));
    }

    [Fact]
    public void Build_WritesTheCutsPerGroup()
    {
        var text = EqApoFile.Build(new TableLevel("VP.X", 100, 66, 0, 0, 0, 0, 40, 120), new GroupLevels(0, 0, 0, 0), Original, false, 0);

        Assert.Contains("Channel: L R\r\nFilter: ON HP Fc 40 Hz\r\n", text);
        Assert.Contains("Channel: RL RR\r\nFilter: ON HP Fc 120 Hz\r\n", text);
        Assert.Contains("Channel: SL SR\r\nFilter: ON HP Fc 120 Hz\r\n", text);
    }

    [Theory]
    [InlineData(null, "Two 90 Hz cuts (original)")]
    [InlineData(0, "Off")]
    [InlineData(60, "60 Hz")]
    public void CabinetLabel_IsPlainWords(int? value, string expected) => Assert.Equal(expected, LowCutChoices.CabinetLabel(value));

    [Fact]
    public void CabinetChoices_GoFromLeastToMostBass()
    {
        Assert.Equal([null, 150, 120, 90, 60, 50, 40, 0], LowCutChoices.CabinetChoices);
    }

    [Fact]
    public void TableLabel_OwnCutIsTheCut_NoOwnCutSaysCabinets()
    {
        Assert.Equal("120 Hz", LowCutChoices.TableLabel(new SoundSettings(ExciterCut: 60), LowCutGroup.Exciters, 120));
        Assert.Equal("Off", LowCutChoices.TableLabel(new SoundSettings(ExciterCut: 60), LowCutGroup.Exciters, 0));
        Assert.Equal("Cabinet's cut (60 Hz)", LowCutChoices.TableLabel(new SoundSettings(ExciterCut: 60), LowCutGroup.Exciters, null));
        Assert.Equal("Cabinet's cut (Off)", LowCutChoices.TableLabel(new SoundSettings(BackboxCut: 0), LowCutGroup.Backbox, null));
        Assert.Equal("Cabinet's cut (2× 90 Hz)", LowCutChoices.TableLabel(Original, LowCutGroup.Backbox, null));
    }

    [Fact]
    public void IsRisky_ATableWithoutItsOwnCut_FollowsCabinetsCut()
    {
        var exciterOff = new SoundSettings(ExciterCut: 0);

        Assert.True(LowCutChoices.IsRisky(exciterOff, LowCutGroup.Exciters, null));
        Assert.False(LowCutChoices.IsRisky(exciterOff, LowCutGroup.Exciters, 90)); // the table's own cut wins
        Assert.False(LowCutChoices.IsRisky(Original, LowCutGroup.Exciters, null));  // two 90 Hz cuts
        Assert.True(LowCutChoices.IsRisky(Original, LowCutGroup.Exciters, 40));
        Assert.True(LowCutChoices.IsRisky(new SoundSettings(ExciterCut: 50), LowCutGroup.Exciters, null));
        Assert.False(LowCutChoices.IsRisky(new SoundSettings(BackboxCut: 0), LowCutGroup.Backbox, null));
    }

    [Fact]
    public void CabinetCuts_SurviveSaveAndLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cabsound-{Guid.NewGuid():N}.json");
        try
        {
            var sound = new SoundSettings(ExciterCut: 60);
            CabSoundDataFile.Save(path, CabSoundData.Defaults() with { Sound = sound });

            Assert.Equal(sound, CabSoundDataFile.Load(path).Sound);
        }
        finally { File.Delete(path); }
    }
}
