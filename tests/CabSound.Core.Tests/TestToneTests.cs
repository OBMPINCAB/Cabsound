using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class TestToneTests
{
    const int Rate = 48000, Channels = 8;

    static float[] Read(TestTone tone, int frames)
    {
        var buf = new float[frames * Channels];
        Assert.Equal(buf.Length, tone.Read(buf, 0, buf.Length));
        return buf;
    }

    [Theory]
    // 7.1 order: FL FR FC LFE BL BR SL SR. Equalizer APO: RL RR = BL BR, SL SR = side.
    // SSF standard (what VPX sends): RL RR = front exciters (near the player), SL SR = rear exciters.
    [InlineData(ToneTarget.Backbox, 0, 1)]
    [InlineData(ToneTarget.FrontExciters, 4, 5)]
    [InlineData(ToneTarget.RearExciters, 6, 7)]
    public void Channels_MatchTheCardOutputs(ToneTarget target, int left, int right)
    {
        Assert.Equal(new[] { left, right }, TestTone.ChannelsFor(target));
    }

    [Fact]
    public void OnlyTargetChannelsPlay()
    {
        var buf = Read(new TestTone(Rate, Channels, ToneTarget.RearExciters, 40, -20), Rate);

        for (var ch = 0; ch < Channels; ch++)
        {
            var peak = Enumerable.Range(0, Rate).Max(f => Math.Abs(buf[f * Channels + ch]));
            if (ch is 6 or 7) Assert.True(peak > 0.09f, $"channel {ch} should play");
            else Assert.Equal(0f, peak);
        }
    }

    [Fact]
    public void Level_IsPeakInDbFullScale()
    {
        var buf = Read(new TestTone(Rate, Channels, ToneTarget.Backbox, 40, -20), Rate);

        var peak = Enumerable.Range(Rate / 2, Rate / 2).Max(f => buf[f * Channels]);
        Assert.InRange(peak, 0.099f, 0.1001f);
    }

    [Fact]
    public void Frequency_IsCorrect()
    {
        var buf = Read(new TestTone(Rate, Channels, ToneTarget.Backbox, 40, -20), Rate);

        // A 40 Hz sine crosses zero upwards 40 times a second
        var upward = Enumerable.Range(1, Rate - 1).Count(f => buf[(f - 1) * Channels] <= 0 && buf[f * Channels] > 0);
        Assert.InRange(upward, 39, 40);
    }

    [Fact]
    public void FadesIn_SoExcitersDontThump()
    {
        var buf = Read(new TestTone(Rate, Channels, ToneTarget.Backbox, 40, -20), Rate / 100);

        // First 10 ms stays well below the full level (fade is 100 ms)
        Assert.True(Enumerable.Range(0, Rate / 100).Max(f => Math.Abs(buf[f * Channels])) < 0.02f);
    }

    [Fact]
    public void ContinuesSmoothlyAcrossReads()
    {
        var whole = Read(new TestTone(Rate, Channels, ToneTarget.Backbox, 40, -20), 2000);
        var split = new TestTone(Rate, Channels, ToneTarget.Backbox, 40, -20);
        var a = Read(split, 1000);
        var b = Read(split, 1000);

        Assert.Equal(whole, a.Concat(b).ToArray());
    }

    [Fact]
    public void SingleChannel_OnlyThatChannelPlays()
    {
        var tone = new TestTone(Rate, Channels, [6], 150, -20);
        var buf = Read(tone, Rate / 2);

        for (var ch = 0; ch < Channels; ch++)
        {
            var peak = Enumerable.Range(0, Rate / 2).Max(f => Math.Abs(buf[f * Channels + ch]));
            if (ch == 6) Assert.True(peak > 0.09f);
            else Assert.Equal(0f, peak);
        }
    }

    [Fact]
    public void Chime_RingsOncePerSecondAndFades()
    {
        var buf = Read(TestTone.Chime(Rate, Channels, [7], -20), Rate * 2);
        float Peak(double fromSec, double toSec) =>
            Enumerable.Range((int)(fromSec * Rate), (int)((toSec - fromSec) * Rate)).Max(f => Math.Abs(buf[f * Channels + 7]));

        Assert.True(Peak(0.0, 0.1) > 0.08f, "loud at the strike");
        Assert.True(Peak(0.8, 0.95) < 0.02f, "faded before the next strike");
        Assert.True(Peak(1.0, 1.1) > 0.08f, "strikes again after 1 s");
        Assert.Equal(0f, Enumerable.Range(0, Rate * 2).Max(f => Math.Abs(buf[f * Channels + 6])));
    }

    [Fact]
    public void Chime_CanStrikeFasterAndAtOtherPitches()
    {
        var buf = Read(TestTone.Chime(Rate, Channels, [4], -20, hz: 1320, strikesPerSecond: 3), Rate);
        float Peak(double fromSec, double toSec) =>
            Enumerable.Range((int)(fromSec * Rate), (int)((toSec - fromSec) * Rate)).Max(f => Math.Abs(buf[f * Channels + 4]));

        Assert.True(Peak(0.333, 0.36) > 0.07f, "second strike at 1/3 s");
        Assert.True(Peak(0.30, 0.33) < Peak(0.333, 0.36), "quieter just before the strike");
        var upward = Enumerable.Range(1, Rate / 3 - 1).Count(f => buf[(f - 1) * Channels + 4] <= 0 && buf[f * Channels + 4] > 0);
        Assert.InRange(upward, 1320 / 3 - 2, 1320 / 3 + 1);
    }

    [Fact]
    public void Pulse_PlaysSmoothTactileBurstAndSilencesBetweenHits()
    {
        var buf = Read(TestTone.Pulse(Rate, Channels, [4, 5, 6, 7], -12, hz: 42, pulseSeconds: 0.25, periodSeconds: 1.0), Rate * 2);
        float Peak(int ch, double fromSec, double toSec) =>
            Enumerable.Range((int)(fromSec * Rate), (int)((toSec - fromSec) * Rate)).Max(f => Math.Abs(buf[f * Channels + ch]));

        Assert.True(Peak(4, 0.08, 0.18) > 0.2f, "strong peak in middle of 250 ms pulse");
        Assert.Equal(0f, Peak(4, 0.30, 0.95)); // completely silent between pulses so decay/mud is audible
        Assert.True(Peak(6, 1.08, 1.18) > 0.2f, "pulses again on second period across all active channels");
        Assert.Equal(0f, Peak(0, 0.0, 2.0));   // backbox untouched
    }

    [Fact]
    public void RefusesLoudLevels()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TestTone(Rate, Channels, ToneTarget.Backbox, 40, -5));
    }

    [Fact]
    public void RefusesDevicesWithoutEnoughChannels()
    {
        Assert.Throws<ArgumentException>(() => new TestTone(Rate, 2, ToneTarget.RearExciters, 40, -20));
    }
}
