using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class ClickTestTests
{
    const int Rate = 48000, Channels = 8;

    // Default order: ch 4 = 1 click, ch 5 = 2, ch 6 = 3, ch 7 = 4; one channel per second
    private static float[] Read(int seconds)
    {
        var buf = new float[Rate * seconds * Channels];
        new ClickTest(Rate, Channels, -20).Read(buf, 0, buf.Length);
        return buf;
    }

    // Counts separate bursts (gaps of > 50 ms of near-silence) on one channel within a time window
    private static int Clicks(float[] buf, int ch, double fromSec, double toSec)
    {
        int count = 0, quiet = Rate;
        for (var f = (int)(fromSec * Rate); f < (int)(toSec * Rate); f++)
        {
            if (Math.Abs(buf[f * Channels + ch]) > 0.01f) { if (quiet > Rate / 20) count++; quiet = 0; }
            else if (quiet < Rate) quiet++;
        }
        return count;
    }

    [Theory]
    [InlineData(4, 0, 1)]
    [InlineData(5, 1, 2)]
    [InlineData(6, 2, 3)]
    [InlineData(7, 3, 4)]
    public void EachChannel_ClicksItsOwnCount_InItsOwnSecond(int ch, int second, int clicks)
    {
        var buf = Read(4);

        Assert.Equal(clicks, Clicks(buf, ch, second, second + 1));
        // silent during the other channels' seconds
        for (var s = 0; s < 4; s++)
            if (s != second) Assert.Equal(0, Clicks(buf, ch, s, s + 1));
    }

    [Fact]
    public void Repeats_EveryFourSeconds()
    {
        var buf = Read(8);

        Assert.Equal(3, Clicks(buf, 6, 6, 7));
    }

    [Fact]
    public void BackboxStaysSilent()
    {
        var buf = Read(4);

        Assert.Equal(0, Clicks(buf, 0, 0, 4));
        Assert.Equal(0, Clicks(buf, 1, 0, 4));
    }
}
