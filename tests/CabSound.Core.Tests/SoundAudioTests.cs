using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class SoundAudioTests
{
    private static VpxSound Sound(int channels, int bits, byte[] data, string path = "x.wav") =>
        VpxSound.Parse(VpxSoundTests.Stream("s", path, channels, bits, data));

    private static byte[] Stereo(Func<int, (short L, short R)> frame, int frames = 1000) =>
        Enumerable.Range(0, frames).SelectMany(i => { var (l, r) = frame(i); return BitConverter.GetBytes(l).Concat(BitConverter.GetBytes(r)); }).ToArray();

    private static short Noise(int i) => (short)(new Random(i).Next(-16000, 16000));

    [Fact]
    public void Facts_IdenticalChannels_CorrelationOne()
    {
        var f = SoundAudio.Facts(Sound(2, 16, Stereo(i => (Noise(i), Noise(i)))))!;

        Assert.Equal(1.0, f.Correlation!.Value, 3);
        Assert.False(f.SumClips);
    }

    [Fact]
    public void Facts_OppositeChannels_CorrelationMinusOne()
    {
        var f = SoundAudio.Facts(Sound(2, 16, Stereo(i => (Noise(i), (short)-Noise(i)))))!;

        Assert.Equal(-1.0, f.Correlation!.Value, 3);
    }

    [Fact]
    public void Facts_LoudStereo_SumClips()
    {
        Assert.True(SoundAudio.Facts(Sound(2, 16, Stereo(_ => (20000, 20000))))!.SumClips);
        Assert.False(SoundAudio.Facts(Sound(2, 16, Stereo(_ => (20000, -20000))))!.SumClips);
    }

    [Fact]
    public void Facts_Silent_PeakMinusInfinity()
    {
        var f = SoundAudio.Facts(Sound(2, 16, new byte[400]))!;

        Assert.Equal(double.NegativeInfinity, f.PeakDb);
        Assert.Null(f.Correlation);
    }

    [Fact]
    public void Facts_Mono_PeakOnly()
    {
        var f = SoundAudio.Facts(Sound(1, 16, [.. BitConverter.GetBytes((short)16384), .. BitConverter.GetBytes((short)-100)]))!;

        Assert.Equal(-6.02, f.PeakDb, 2);
        Assert.Null(f.Correlation);
        Assert.False(f.SumClips);
    }

    [Fact]
    public void Facts_Pcm24_DetectsSilentAndActiveAudio()
    {
        var silent24 = SoundAudio.Facts(Sound(2, 24, new byte[12]))!;
        Assert.Equal(double.NegativeInfinity, silent24.PeakDb);

        // Half-scale positive 24-bit sample (+4194304 = 0x400000) -> ~ -6.02 dBFS
        var half24 = SoundAudio.Facts(Sound(1, 24, [0x00, 0x00, 0x40, 0x00, 0x00, 0x00]))!;
        Assert.Equal(-6.02, half24.PeakDb, 2);
    }

    [Fact]
    public void Facts_UnsupportedFormats_Unknown_NoThrow()
    {
        Assert.Null(SoundAudio.Facts(Sound(1, 8, [1, 2, 3])));
        Assert.Null(SoundAudio.Facts(Sound(2, 24, new byte[10])));  // not whole 6-byte stereo frames
        Assert.Null(SoundAudio.Facts(Sound(2, 16, new byte[6])));   // not whole frames
        Assert.Null(SoundAudio.Facts(Sound(2, 16, [])));
        Assert.Null(SoundAudio.Facts(Sound(2, 16, [1, 2, 3, 4], "x.ogg")));
    }

    [Fact]
    public void Info_CarriesAudioFacts()
    {
        Assert.NotNull(Sound(2, 16, Stereo(i => (Noise(i), Noise(i)))).Info.Audio);
    }
}
