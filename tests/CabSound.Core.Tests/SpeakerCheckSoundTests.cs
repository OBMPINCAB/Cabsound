using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class SpeakerCheckSoundTests
{
    const int Rate = 48000, Channels = 8;
    static readonly float[] Word = Enumerable.Repeat(0.5f, 4800).ToArray();   // 0.1 s
    static readonly float Peak = (float)Math.Pow(10, -12 / 20.0);             // 0.2512

    /// <summary>Reads the given seconds and returns one channel's samples.</summary>
    static float[] Read(SpeakerCheckSound sound, double seconds, int channel)
    {
        var buffer = new float[(int)(seconds * Rate) * Channels];
        Assert.Equal(buffer.Length, sound.Read(buffer, 0, buffer.Length));
        return Enumerable.Range(0, buffer.Length / Channels).Select(f => buffer[f * Channels + channel]).ToArray();
    }

    static float Max(float[] samples, double from, double to) =>
        samples[(int)(from * Rate)..(int)(to * Rate)].Max(Math.Abs);

    [Fact]
    public void OnlyTheChosenChannelsSound()
    {
        for (var ch = 0; ch < Channels; ch++)
        {
            var samples = Read(new SpeakerCheckSound(Rate, Channels, [4], Word), 3, ch);
            if (ch == 4) Assert.Contains(samples, s => s != 0);
            else Assert.All(samples, s => Assert.Equal(0, s));
        }
    }

    [Fact]
    public void PeakIsMinus12Dbfs()
    {
        var samples = Read(new SpeakerCheckSound(Rate, Channels, [4], Word), 1.5, 4);

        Assert.InRange(Max(samples, 0, 0.63), 0.15f, Peak + 0.0001f);       // clacks: noise, so a little under
        Assert.InRange(Max(samples, 0.87, 0.97), Peak - 0.001f, Peak + 0.0001f); // word: scaled to the peak
    }

    [Fact]
    public void ThreeClacks300MsApartThenSilenceThenWord()
    {
        var samples = Read(new SpeakerCheckSound(Rate, Channels, [4], Word), 1.5, 4);

        Assert.True(Max(samples, 0, 0.02) > 0);
        Assert.True(Max(samples, 0.30, 0.32) > 0);
        Assert.True(Max(samples, 0.60, 0.62) > 0);
        Assert.Equal(0, Max(samples, 0.03, 0.29));
        Assert.Equal(0, Max(samples, 0.33, 0.59));
        Assert.Equal(0, Max(samples, 0.63, 0.86));
        Assert.True(Max(samples, 0.87, 0.97) > 0);
    }

    [Fact]
    public void RepeatsAfterThreeSeconds()
    {
        var samples = Read(new SpeakerCheckSound(Rate, Channels, [4], Word), 5, 4);

        Assert.Equal(0, Max(samples, 0.98, 3.96));
        Assert.True(Max(samples, 3.97, 3.99) > 0);
    }

    [Fact]
    public void WithoutAWord_ClacksOnlyAndRepeats()
    {
        var samples = Read(new SpeakerCheckSound(Rate, Channels, [4], null), 5, 4);

        Assert.Equal(0, Max(samples, 0.63, 3.61));
        Assert.True(Max(samples, 3.62, 3.64) > 0);
    }

    [Fact]
    public void SilentWord_CountsAsNoWord()
    {
        var samples = Read(new SpeakerCheckSound(Rate, Channels, [4], new float[4800]), 5, 4);

        Assert.True(Max(samples, 3.62, 3.64) > 0);
    }

    [Fact]
    public void TwoChannels_BothSound()
    {
        Assert.True(Max(Read(new SpeakerCheckSound(Rate, Channels, [0, 1], Word), 1, 0), 0, 0.02) > 0);
        Assert.True(Max(Read(new SpeakerCheckSound(Rate, Channels, [0, 1], Word), 1, 1), 0, 0.02) > 0);
    }

    [Fact]
    public void NeedsEightChannels()
    {
        var sound = new SpeakerCheckSound(Rate, 2, [0], Word);
        Assert.Throws<ArgumentException>(() => sound.Read(new float[200], 0, 200));
    }
}
