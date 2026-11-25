namespace CabSound.Core;

/// <summary>Facts about a sound's audio. PeakDb: loudest sample in dBFS (-infinity = all zeros). Correlation: L vs R
/// (stereo only; -1 = opposite phase, so VPX's L+R nearly cancels). SumClips: L+R goes past full scale (VPX sums L+R).</summary>
public sealed record AudioFacts(double PeakDb, double? Correlation, bool SumClips);

public static class SoundAudio
{
    /// <summary>Facts for 16-bit or 24-bit PCM .wav sounds with 1 or 2 channels; null for anything else (never throws).</summary>
    public static AudioFacts? Facts(VpxSound s)
    {
        if (!s.IsWav || s.FormatTag != 1 || s.Bits is not (16 or 24) || s.Channels is not (1 or 2)) return null;
        var bytesPerSample = s.Bits / 8;
        var frameBytes = bytesPerSample * s.Channels;
        if (s.Data.Length == 0 || s.Data.Length % frameBytes != 0) return null;

        double fullScale = s.Bits == 16 ? 32768.0 : 8388608.0;
        int clipLimit = s.Bits == 16 ? 32767 : 8388607;
        var frames = s.Data.Length / frameBytes;
        long peak = 0, sumPeak = 0;
        double sl = 0, sr = 0, sll = 0, srr = 0, slr = 0;

        static int ReadSample(byte[] data, int offset, int bytes)
        {
            if (bytes == 2) return BitConverter.ToInt16(data, offset);
            var v = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
            return v >= 0x800000 ? v - 0x1000000 : v;
        }

        for (var i = 0; i < frames; i++)
        {
            int l = ReadSample(s.Data, i * frameBytes, bytesPerSample);
            peak = Math.Max(peak, Math.Abs((long)l));
            if (s.Channels == 1) continue;
            int r = ReadSample(s.Data, i * frameBytes + bytesPerSample, bytesPerSample);
            peak = Math.Max(peak, Math.Abs((long)r));
            sumPeak = Math.Max(sumPeak, Math.Abs((long)l + r));
            sl += l; sr += r; sll += (double)l * l; srr += (double)r * r; slr += (double)l * r;
        }

        var peakDb = peak == 0 ? double.NegativeInfinity : 20 * Math.Log10(peak / fullScale);
        if (s.Channels == 1) return new(peakDb, null, false);

        var varL = sll - sl * sl / frames;
        var varR = srr - sr * sr / frames;
        double? correlation = varL <= 0 || varR <= 0 ? null : (slr - sl * sr / frames) / Math.Sqrt(varL * varR);
        return new(peakDb, correlation, sumPeak > clipLimit);
    }
}
