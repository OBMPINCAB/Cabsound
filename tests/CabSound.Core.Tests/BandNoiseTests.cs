using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class BandNoiseTests
{
    const int Rate = 48000, Channels = 8;

    static float[] Read(BandNoise noise, int frames)
    {
        var buf = new float[frames * Channels];
        Assert.Equal(buf.Length, noise.Read(buf, 0, buf.Length));
        return buf;
    }

    static double Rms(float[] buf, int channel, int fromFrame, int frames)
    {
        double sum = 0;
        for (var f = fromFrame; f < fromFrame + frames; f++) sum += buf[f * Channels + channel] * (double)buf[f * Channels + channel];
        return Math.Sqrt(sum / frames);
    }

    static double Db(double rms) => 20 * Math.Log10(rms);

    /// <summary>Share of a channel's energy between loHz and hiHz: plain DFT (Hann window) over 8192 frames after the fade.</summary>
    static double BandShare(float[] buf, int channel, double loHz, double hiHz)
    {
        const int n = 8192;
        var from = Rate; // 1 s in: well after the fade
        double inside = 0, total = 0;
        for (var k = 1; k < n / 2; k++)
        {
            double re = 0, im = 0;
            for (var i = 0; i < n; i++)
            {
                var window = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / n);
                var x = buf[(from + i) * Channels + channel] * window;
                re += x * Math.Cos(2 * Math.PI * k * i / n);
                im -= x * Math.Sin(2 * Math.PI * k * i / n);
            }
            var power = re * re + im * im;
            var hz = (double)k * Rate / n;
            total += power;
            if (hz >= loHz && hz <= hiHz) inside += power;
        }
        return inside / total;
    }

    [Fact]
    public void OnlyActiveChannelsPlay()
    {
        var buf = Read(BandNoise.Level(Rate, Channels, [4, 5]), Rate);

        for (var ch = 0; ch < Channels; ch++)
        {
            var peak = Enumerable.Range(0, Rate).Max(f => Math.Abs(buf[f * Channels + ch]));
            if (ch is 4 or 5) Assert.True(peak > 0f, $"channel {ch} should play");
            else Assert.Equal(0f, peak);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LevelIsMinus20DbfsAfterTheFade(bool low)
    {
        var noise = low ? BandNoise.Low(Rate, Channels, [4, 5]) : BandNoise.Level(Rate, Channels, [4, 5]);
        var buf = Read(noise, Rate * 5 / 2);

        foreach (var ch in new[] { 4, 5 })
            Assert.InRange(Db(Rms(buf, ch, Rate / 2, Rate * 2)), -21, -19);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NeverClips(bool low)
    {
        var noise = low ? BandNoise.Low(Rate, Channels, [4, 5]) : BandNoise.Level(Rate, Channels, [4, 5]);
        var buf = Read(noise, Rate * 5);

        Assert.True(buf.Max(Math.Abs) < 1f);
    }

    [Fact]
    public void TwoChannelsOfAPairDiffer()
    {
        var buf = Read(BandNoise.Level(Rate, Channels, [4, 5]), Rate * 5 / 2);

        double sum = 0;
        for (var f = Rate / 2; f < Rate * 5 / 2; f++) sum += buf[f * Channels + 4] * (double)buf[f * Channels + 5];
        var correlation = sum / (Rate * 2) / (Rms(buf, 4, Rate / 2, Rate * 2) * Rms(buf, 5, Rate / 2, Rate * 2));
        Assert.InRange(correlation, -0.2, 0.2);
    }

    [Fact]
    public void FadesIn()
    {
        var buf = Read(BandNoise.Level(Rate, Channels, [4, 5]), Rate * 5 / 2);

        Assert.True(Rms(buf, 4, 0, Rate / 20) < Rms(buf, 4, Rate / 2, Rate * 2) / 4);
    }

    [Fact]
    public void LevelNoiseStaysInItsBand()
    {
        var buf = Read(BandNoise.Level(Rate, Channels, [4, 5]), Rate * 2);

        Assert.True(BandShare(buf, 4, 250, 4000) > 0.95);
        Assert.True(BandShare(buf, 4, 500, 2000) > 0.5, "most energy inside the nominal band");
    }

    [Fact]
    public void LowNoiseStaysInItsBand()
    {
        var buf = Read(BandNoise.Low(Rate, Channels, [4, 5]), Rate * 2);

        Assert.True(BandShare(buf, 4, 15, 400) > 0.95);
    }

    [Fact]
    public void SameSeedSameSamples()
    {
        Assert.Equal(Read(BandNoise.Level(Rate, Channels, [4, 5], seed: 7), 4000),
            Read(BandNoise.Level(Rate, Channels, [4, 5], seed: 7), 4000));
    }

    [Fact]
    public void DifferentSeedDifferentSamples()
    {
        Assert.NotEqual(Read(BandNoise.Level(Rate, Channels, [4, 5], seed: 7), 4000),
            Read(BandNoise.Level(Rate, Channels, [4, 5], seed: 8), 4000));
    }

    [Fact]
    public void ContinuesSmoothlyAcrossReads()
    {
        var whole = Read(BandNoise.Level(Rate, Channels, [4, 5]), 2000);
        var split = BandNoise.Level(Rate, Channels, [4, 5]);

        Assert.Equal(whole, Read(split, 1000).Concat(Read(split, 1000)).ToArray());
    }

    [Fact]
    public void SingleChannelWorks()
    {
        var buf = Read(BandNoise.Level(Rate, Channels, [6]), Rate);

        for (var ch = 0; ch < Channels; ch++)
        {
            var peak = Enumerable.Range(0, Rate).Max(f => Math.Abs(buf[f * Channels + ch]));
            if (ch == 6) Assert.True(peak > 0f);
            else Assert.Equal(0f, peak);
        }
    }

    [Fact]
    public void TooFewChannels_Throws()
    {
        Assert.Throws<ArgumentException>(() => BandNoise.Level(Rate, 2, [4]));
    }
}
