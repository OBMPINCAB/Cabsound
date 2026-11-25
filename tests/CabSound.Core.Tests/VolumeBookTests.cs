using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class VolumeBookTests
{
    // Cut down to -10, boost up to +3
    private static VolumeBook Book() => new(new[]
    {
        new TableLevel("System", 100, 66, 0, 0, 0),
        new TableLevel("VP.Fish Tales", 43, 71, -10, 2, 0),
    }, cutLimitDb: 10, boostLimitDb: 3);

    [Fact]
    public void Get_KnownTable_ReturnsSavedLevels()
    {
        Assert.Equal(43, Book().Get("VP.Fish Tales").Volume);
    }

    [Fact]
    public void Get_NewTable_GetsSystemVolumeAndZeroOffsets()
    {
        var t = Book().Get("VP.Brand New");

        Assert.Equal(new TableLevel("VP.Brand New", 100, 66, 0, 0, 0), t);
    }

    [Fact]
    public void ChangeSsf_BeyondLimit_CanOnlyMoveBackTowardIt()
    {
        // Imported tables can sit beyond the limits (exact PinVol levels kept)
        var b = new VolumeBook(new[] { new TableLevel("VP.Totan", 100, 66, 20, -13, 6) }, cutLimitDb: 10, boostLimitDb: 3);

        Assert.Equal(20, b.ChangeSsf("VP.Totan", SsfGroup.Backglass, +1).BackglassDb);
        Assert.Equal(19, b.ChangeSsf("VP.Totan", SsfGroup.Backglass, -1).BackglassDb);
        Assert.Equal(-13, b.ChangeSsf("VP.Totan", SsfGroup.Rear, -1).RearDb);
        Assert.Equal(-12, b.ChangeSsf("VP.Totan", SsfGroup.Rear, +1).RearDb);
        Assert.Equal(6, b.ChangeSsf("VP.Totan", SsfGroup.Front, +1).FrontDb);
        Assert.Equal(5, b.ChangeSsf("VP.Totan", SsfGroup.Front, -1).FrontDb);
    }

    [Fact]
    public void ChangeVolume_ClampsBetween0And100()
    {
        var b = Book();

        Assert.Equal(48, b.ChangeVolume("VP.Fish Tales", +5).Volume);
        Assert.Equal(100, b.ChangeVolume("VP.Fish Tales", +500).Volume);
        Assert.Equal(0, b.ChangeVolume("VP.Fish Tales", -500).Volume);
    }

    [Fact]
    public void ChangeSsf_ClampsToBoostAndCutLimits()
    {
        var b = Book();

        Assert.Equal(3, b.ChangeSsf("VP.Fish Tales", SsfGroup.Front, +5).FrontDb);
        Assert.Equal(-10, b.ChangeSsf("VP.Fish Tales", SsfGroup.Backglass, -1).BackglassDb);
        Assert.Equal(1, b.ChangeSsf("VP.Fish Tales", SsfGroup.Rear, -1).RearDb);
    }

    [Fact]
    public void ChangeSsf_SubGroup_ChangesSubLevel()
    {
        var b = Book();

        Assert.Equal(-1, b.ChangeSsf("VP.Fish Tales", SsfGroup.Sub, -1).SubDb);
        Assert.Equal(-10, b.ChangeSsf("VP.Fish Tales", SsfGroup.Sub, -50).SubDb);
    }

    [Fact]
    public void IsAtSsfLimit_AtBoostLimit()
    {
        var b = new VolumeBook(new[] { new TableLevel("VP.X", 100, 66, 0, 2, 0) }, cutLimitDb: 10, boostLimitDb: 3);
        Assert.False(b.IsAtSsfLimit("VP.X"));

        b.ChangeSsf("VP.X", SsfGroup.Rear, +1);

        Assert.True(b.IsAtSsfLimit("VP.X"));
    }

    [Fact]
    public void IsAtSsfLimit_AtCutLimit()
    {
        Assert.True(Book().IsAtSsfLimit("VP.Fish Tales")); // backbox -10
    }

    [Fact]
    public void Changes_AreSavedAndNewTablesAppendedInOrder()
    {
        var b = Book();
        b.ChangeVolume("VP.Brand New", -10);

        var all = b.All();

        Assert.Equal(new[] { "System", "VP.Fish Tales", "VP.Brand New" }, all.Select(t => t.Name));
        Assert.Equal(90, all[2].Volume);
    }

    [Fact]
    public void Set_StoresBeyondLimitValuesAsGiven()
    {
        var b = Book();

        b.Set(new TableLevel("VP.Fish Tales", 43, 71, -10, 2, 6));

        Assert.Equal(6, b.Get("VP.Fish Tales").FrontDb);
    }
}
