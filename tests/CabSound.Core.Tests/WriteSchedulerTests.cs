using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class WriteSchedulerTests
{
    [Fact]
    public void Include_RapidChangesAreOneWrite_DueAfterDelayFromTheFirst()
    {
        var s = new WriteScheduler();
        s.IncludeChanged(0);
        s.IncludeChanged(200);

        Assert.False(s.Due(299).Include);
        Assert.True(s.Due(300).Include);
        Assert.False(s.Due(600).Include);
    }

    [Fact]
    public void Include_KeepsWritingWhileAKeyIsHeld()
    {
        // Hold-to-repeat changes the level every 100 ms: the sound must follow, not wait for the release
        var s = new WriteScheduler();
        var writes = 0;
        for (var now = 0; now <= 1000; now += 20)
        {
            if (now % 100 == 0) s.IncludeChanged(now);
            if (s.Due(now).Include) writes++;
        }

        Assert.True(writes >= 2, $"only {writes} writes in 1 s"); // was 0 until the key was released
    }

    [Fact]
    public void Data_DueAfterTwoSeconds()
    {
        var s = new WriteScheduler();
        s.DataChanged(0);

        Assert.False(s.Due(1999).Data);
        Assert.True(s.Due(2000).Data);
    }

    [Fact]
    public void TakeAll_ReturnsPendingAndClears()
    {
        var s = new WriteScheduler();
        s.IncludeChanged(0);
        s.DataChanged(0);

        Assert.Equal((true, true), s.TakeAll());
        Assert.Equal((false, false), s.TakeAll());
    }

    [Fact]
    public void NothingChanged_NothingDue()
    {
        Assert.Equal((false, false), new WriteScheduler().Due(10_000));
    }
}
