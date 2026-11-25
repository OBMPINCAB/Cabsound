using System.Text;
using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class VpxSoundTests
{
    // Builds a GameStg/SoundN stream the way VPX 10.8 writes it.
    internal static byte[] Stream(string name, string path, int channels, int bits, byte[] data, int output = 0, int rate = 44100)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        void Str(string s) { var b = Encoding.Latin1.GetBytes(s); w.Write(b.Length); w.Write(b); }
        Str(name); Str(path); Str("\0");
        if (path.EndsWith(".wav"))
        {
            var block = channels * bits / 8;
            w.Write((ushort)1); w.Write((ushort)channels); w.Write(rate); w.Write(rate * block);
            w.Write((ushort)block); w.Write((ushort)bits); w.Write((ushort)0);
        }
        w.Write(data.Length); w.Write(data);
        w.Write((byte)output); w.Write(-79); w.Write(5); w.Write(-3); w.Write(0);
        return ms.ToArray();
    }

    private static byte[] Pcm16(params short[] samples) => samples.SelectMany(BitConverter.GetBytes).ToArray();

    [Fact]
    public void Parse_ReadsWavFormatAndOutput()
    {
        var s = VpxSound.Parse(Stream("fx_Bumper", "C:\\snd\\bumper.wav", 2, 16, Pcm16(1, 2, 3, 4), output: 1));

        Assert.Equal("fx_Bumper", s.Name);
        Assert.Equal("C:\\snd\\bumper.wav", s.Path);
        Assert.True(s.IsWav);
        Assert.Equal(2, s.Channels);
        Assert.Equal(16, s.Bits);
        Assert.Equal(44100, s.Rate);
        Assert.Equal(SoundOutput.Backglass, s.Output);
        Assert.Equal(8, s.Data.Length);
    }

    [Fact]
    public void Parse_NonWav_KeepsDataAndOutput()
    {
        var s = VpxSound.Parse(Stream("music", "song.ogg", 0, 0, [9, 9, 9], output: 0));

        Assert.False(s.IsWav);
        Assert.Equal(SoundOutput.Table, s.Output);
        Assert.Equal(3, s.Data.Length);
        Assert.False(s.CanMakeMono);
    }

    [Fact]
    public void Write_RoundTripsExactly()
    {
        var bytes = Stream("rubber", "rubber.wav", 2, 16, Pcm16(100, -100, 7, 9));
        Assert.Equal(bytes, VpxSound.Parse(bytes).Write());
    }

    [Fact]
    public void MakeMono_AveragesChannels_FixesFormat_KeepsSettings()
    {
        var stereo = VpxSound.Parse(Stream("sling", "sling.wav", 2, 16, Pcm16(1000, 3000, -32768, -32768, 32767, 32767)));

        var (mono, peakIn, peakOut) = stereo.MakeMono();

        Assert.Equal(1, mono.Channels);
        Assert.Equal(Pcm16(2000, -32768, 32767), mono.Data);
        Assert.Equal(32768, peakIn);
        Assert.Equal(32768, peakOut);
        var expected = Stream("sling", "sling.wav", 1, 16, Pcm16(2000, -32768, 32767));
        Assert.Equal(expected, mono.Write()); // block align, bytes/s and the 17-byte tail all right
    }

    [Theory]
    [InlineData(2, 16, 0, true)]
    [InlineData(1, 16, 0, false)]  // already mono
    [InlineData(2, 24, 0, false)]  // 24-bit not handled
    [InlineData(2, 16, 1, false)]  // backglass sounds stay stereo
    public void CanMakeMono_OnlyStereo16BitTableSounds(int channels, int bits, int output, bool expected)
    {
        var data = new byte[channels * bits / 8 * 2];
        Assert.Equal(expected, VpxSound.Parse(Stream("s", "s.wav", channels, bits, data, output)).CanMakeMono);
    }

    [Fact]
    public void WithOutput_ChangesOnlyTheOutputByte()
    {
        var bytes = Stream("SFX_GunFire", "gun.wav", 2, 16, Pcm16(1, 2, 3, 4), output: 0);

        var moved = VpxSound.Parse(bytes).WithOutput(SoundOutput.Backglass);

        Assert.Equal(SoundOutput.Backglass, moved.Output);
        Assert.Equal(Stream("SFX_GunFire", "gun.wav", 2, 16, Pcm16(1, 2, 3, 4), output: 1), moved.Write());
        Assert.False(moved.CanMakeMono); // backglass sounds stay stereo
    }

    [Fact]
    public void WithAudioFrom_TakesOldAudioKeepsCurrentOutput()
    {
        var oldStereo = VpxSound.Parse(Stream("gameloop", "loop.wav", 2, 16, Pcm16(1, 2, 3, 4), output: 0));
        var nowMonoOnBackbox = VpxSound.Parse(Stream("gameloop", "loop.wav", 1, 16, Pcm16(1, 3), output: 1));

        var restored = nowMonoOnBackbox.WithAudioFrom(oldStereo);

        Assert.Equal(Stream("gameloop", "loop.wav", 2, 16, Pcm16(1, 2, 3, 4), output: 1), restored.Write());
    }

    [Fact]
    public void WithAudioFrom_OtherSound_Throws()
    {
        var other = VpxSound.Parse(Stream("endgame", "end.wav", 2, 16, Pcm16(1, 2), output: 0));
        var now = VpxSound.Parse(Stream("gameloop", "loop.wav", 1, 16, Pcm16(1), output: 1));

        Assert.Throws<ArgumentException>(() => now.WithAudioFrom(other));
    }

    [Theory]
    [InlineData("SFX_GunFire", true)]
    [InlineData("sfx_gunfire", true)]   // case-insensitive
    [InlineData("FX_SpeciesTrackerLaunch", true)]
    [InlineData("fx_autoplunger", false)]
    [InlineData("SFX", false)]
    public void NameMatches_ExactOrPrefixWithStar(string name, bool expected)
    {
        Assert.Equal(expected, VpxSound.NameMatches(name, ["SFX_*", "FX_SpeciesTrackerLaunch"]));
    }

    [Fact]
    public void WithShakerPulse_ReplacesSilentDummyWith38HzMonoTablePulse()
    {
        var silentDummy = VpxSound.Parse(Stream("SaucerShake", "Z:\\SaucerShake.wav", 2, 24, new byte[2400], output: 1, rate: 44100));

        var shaker = silentDummy.WithShakerPulse(seconds: 0.5, hz: 38, peakDb: -1.5);

        Assert.Equal("SaucerShake", shaker.Name);
        Assert.Equal("Z:\\SaucerShake.wav", shaker.Path);
        Assert.Equal(SoundOutput.Table, shaker.Output);
        Assert.Equal(1, shaker.Channels);
        Assert.Equal(16, shaker.Bits);
        Assert.Equal(44100, shaker.Rate);
        var facts = SoundAudio.Facts(shaker)!;
        Assert.InRange(facts.PeakDb, -1.7, -1.3);
        Assert.Equal(shaker.Write(), VpxSound.Parse(shaker.Write()).Write());
    }

    [Fact]
    public void WithShakerUnderlay_KeepsExistingRattleAndAdds38HzBassWithoutClipping()
    {
        // High-frequency 2 kHz rattle at -6 dBFS (16384) with zero sub-bass
        var frames = 44100 / 2;
        var rattlePcm = Enumerable.Range(0, frames)
            .Select(i => (short)Math.Round(16384 * Math.Sin(2 * Math.PI * 2000 * i / 44100.0)))
            .ToArray();
        var original = VpxSound.Parse(Stream("SaucerShake", "ufo.wav", 1, 16, Pcm16(rattlePcm), output: 0, rate: 44100));

        var mixed = original.WithShakerUnderlay(hz: 38, underlayDb: -6);

        Assert.Equal("SaucerShake", mixed.Name);
        Assert.Equal(SoundOutput.Table, mixed.Output);
        Assert.Equal(1, mixed.Channels);
        Assert.Equal(16, mixed.Bits);
        var facts = SoundAudio.Facts(mixed)!;
        Assert.InRange(facts.PeakDb, -2.0, 0.0); // never clips, louder than -6 dBFS alone

        // Verify both 38 Hz shaker fundamental AND 2000 Hz plastic rattle remain strong
        double GoertzelDb(double freq)
        {
            var w = 2 * Math.PI * freq / 44100.0;
            var coeff = 2 * Math.Cos(w);
            double s1 = 0, s2 = 0;
            for (var i = 0; i < frames; i++)
            {
                var x = BitConverter.ToInt16(mixed.Data, i * 2) / 32768.0;
                var s0 = x + coeff * s1 - s2;
                s2 = s1; s1 = s0;
            }
            var mag = Math.Sqrt(s1 * s1 + s2 * s2 - coeff * s1 * s2) / (frames / 2.0);
            return 20 * Math.Log10(mag);
        }

        Assert.True(GoertzelDb(38) > -12, "38 Hz shaker layer present");
        Assert.True(GoertzelDb(2000) > -12, "original 2 kHz toy rattle preserved");
    }

    [Fact]
    public void Parse_BadTail_Throws()
    {
        var bytes = Stream("s", "s.wav", 1, 16, Pcm16(1));
        Assert.Throws<InvalidDataException>(() => VpxSound.Parse(bytes[..^1]));
    }
}
