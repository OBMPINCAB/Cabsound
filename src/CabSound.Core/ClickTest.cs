namespace CabSound.Core;

/// <summary>
/// Exciter channel test by ear: short clicks (noise bursts) don't ring through the playfield like
/// tones do. Each exciter channel gets its own second and its own count: ch 4 (RL) 1 click,
/// ch 5 (RR) 2, ch 6 (SL) 3, ch 7 (SR) 4, repeating every 4 s. The listener says which count is
/// loudest at each corner. Interleaved float samples, like TestTone.
/// </summary>
public sealed class ClickTest(int sampleRate, int channels, double levelDb)
{
    private static readonly int[] Order = [4, 5, 6, 7];
    private const double FirstClick = 0.05, Spacing = 0.15, ClickLength = 0.02;

    private readonly double _amplitude = levelDb <= TestTone.MaxLevelDb
        ? Math.Pow(10, levelDb / 20)
        : throw new ArgumentOutOfRangeException(nameof(levelDb), $"Max is {TestTone.MaxLevelDb} dBFS");
    private readonly Random _noise = new(1234);
    private long _frame;

    public int Read(float[] buffer, int offset, int count)
    {
        if (channels < 8) throw new ArgumentException("Needs a 7.1 device (8 channels)");
        var frames = count / channels;
        Array.Clear(buffer, offset, frames * channels);
        for (var f = 0; f < frames; f++, _frame++)
        {
            var slot = (int)(_frame / sampleRate % Order.Length);
            var t = (double)(_frame % sampleRate) / sampleRate - FirstClick;
            var click = (int)Math.Floor(t / Spacing);
            var tInClick = t - click * Spacing;
            if (t < 0 || click > slot || tInClick > ClickLength) continue;

            var noise = _noise.NextDouble() * 2 - 1;
            buffer[offset + f * channels + Order[slot]] = (float)(_amplitude * noise * Math.Exp(-tInClick * 500));
        }
        return frames * channels;
    }
}
