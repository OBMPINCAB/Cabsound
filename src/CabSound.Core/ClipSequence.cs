namespace CabSound.Core;

/// <summary>
/// Plays mono clips (e.g. a spoken "left rear") one after another, each on its own channel, with a
/// gap after each, looping. For the exciter wiring check. Interleaved float samples, like TestTone.
/// </summary>
public sealed class ClipSequence(int channels, IReadOnlyList<(float[] Samples, int Channel)> clips, int gapFrames)
{
    private int _clip, _pos;

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / channels;
        Array.Clear(buffer, offset, frames * channels);
        for (var f = 0; f < frames; f++)
        {
            var (samples, channel) = clips[_clip];
            if (_pos < samples.Length) buffer[offset + f * channels + channel] = samples[_pos];
            if (++_pos >= samples.Length + gapFrames) { _pos = 0; _clip = (_clip + 1) % clips.Count; }
        }
        return frames * channels;
    }
}
