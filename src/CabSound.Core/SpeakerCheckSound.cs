namespace CabSound.Core;

/// <summary>
/// The speaker check's test sound: three sharp clacks 300 ms apart, a pause, a spoken word, a 3 s gap, and
/// again (slow on purpose: the owner listens at his own pace). The clacks are what the ear places (a voice's
/// low part travels through the cabinet, cabinet 2026-11-11); the word makes it easy to notice.
/// word = mono samples at the sample rate; null or silent = clacks only. Interleaved floats, like ClickTest.
/// </summary>
public sealed class SpeakerCheckSound(int sampleRate, int channels, int[] playOn, float[]? word)
{
    public const double LevelDb = -12;
    private const double Spacing = 0.30, ClackLength = 0.02, PauseBeforeWord = 0.25, Gap = 3.0;
    private const int Clacks = 3;
    private const double ClacksEnd = (Clacks - 1) * Spacing + ClackLength;   // 0.62 s

    private static readonly double Amplitude = Math.Pow(10, LevelDb / 20);
    private readonly Random _noise = new(1234);
    private readonly float[] _word = Scaled(word);
    private long _frame;

    public int Read(float[] buffer, int offset, int count)
    {
        if (channels < 8) throw new ArgumentException("Needs a 7.1 device (8 channels)");
        var wordStart = (long)Math.Round((ClacksEnd + PauseBeforeWord) * sampleRate);
        var soundEnd = _word.Length > 0 ? wordStart + _word.Length : (long)Math.Round(ClacksEnd * sampleRate);
        var period = soundEnd + (long)Math.Round(Gap * sampleRate);

        var frames = count / channels;
        Array.Clear(buffer, offset, frames * channels);
        for (var f = 0; f < frames; f++, _frame++)
        {
            var at = _frame % period;
            var sample = at >= wordStart && at - wordStart < _word.Length ? _word[at - wordStart] : Clack((double)at / sampleRate);
            if (sample == 0) continue;
            foreach (var ch in playOn) buffer[offset + f * channels + ch] = sample;
        }
        return frames * channels;
    }

    /// <summary>ClickTest's burst: 20 ms of noise that dies away fast.</summary>
    private float Clack(double t)
    {
        var clack = (int)(t / Spacing);
        var tInClack = t - clack * Spacing;
        if (clack >= Clacks || tInClack > ClackLength) return 0;
        return (float)(Amplitude * (_noise.NextDouble() * 2 - 1) * Math.Exp(-tInClack * 500));
    }

    /// <summary>The word with its own peak at the level; empty when there is none or it is silent.</summary>
    private static float[] Scaled(float[]? word)
    {
        var peak = word is { Length: > 0 } ? word.Max(Math.Abs) : 0;
        return peak == 0 ? [] : word!.Select(s => (float)(s / peak * Amplitude)).ToArray();
    }
}
