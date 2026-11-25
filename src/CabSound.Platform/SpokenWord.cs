using System.Speech.AudioFormat;
using System.Speech.Synthesis;

namespace CabSound.Platform;

/// <summary>
/// A word spoken by Windows' own voice, as mono samples at the given rate (for the speaker check's test
/// sound). Silence before and after the word is cut off. null when Windows has no voice or speech fails:
/// the caller then goes without the word.
/// </summary>
public static class SpokenWord
{
    public static float[]? Render(string text, int sampleRate)
    {
        try
        {
            using var stream = new MemoryStream();
            using (var voice = new SpeechSynthesizer())
            {
                voice.SetOutputToAudioStream(stream, new SpeechAudioFormatInfo(sampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
                voice.Speak(text);
                voice.SetOutputToNull();
            }

            var bytes = stream.ToArray();
            var samples = new float[bytes.Length / 2];
            for (var i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;
            return Trimmed(samples);
        }
        catch (Exception)
        {
            return null;   // no voice installed, speech service off, ...
        }
    }

    /// <summary>Without the quiet lead-in and tail (under 2 % of the peak); null when it is all silence.</summary>
    private static float[]? Trimmed(float[] samples)
    {
        var floor = samples.Length == 0 ? 0 : samples.Max(Math.Abs) * 0.02f;
        if (floor == 0) return null;
        var first = Array.FindIndex(samples, s => Math.Abs(s) > floor);
        var last = Array.FindLastIndex(samples, s => Math.Abs(s) > floor);
        return samples[first..(last + 1)];
    }
}
