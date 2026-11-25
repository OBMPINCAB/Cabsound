using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class ClipSequenceTests
{
    const int Channels = 8;

    private static float[] Clip(int length, float value) => Enumerable.Repeat(value, length).ToArray();

    private static float[] Read(ClipSequence s, int frames)
    {
        var buf = new float[frames * Channels];
        Assert.Equal(buf.Length, s.Read(buf, 0, buf.Length));
        return buf;
    }

    [Fact]
    public void PlaysEachClipOnItsChannel_InTurn_WithAGap()
    {
        var s = new ClipSequence(Channels, [(Clip(3, 0.5f), 4), (Clip(2, 0.25f), 5)], gapFrames: 2);
        var buf = Read(s, 9);
        float At(int frame, int ch) => buf[frame * Channels + ch];

        Assert.Equal([0.5f, 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f], Enumerable.Range(0, 9).Select(f => At(f, 4)));
        Assert.Equal([0f, 0f, 0f, 0f, 0f, 0.25f, 0.25f, 0f, 0f], Enumerable.Range(0, 9).Select(f => At(f, 5)));
    }

    [Fact]
    public void Loops()
    {
        var s = new ClipSequence(Channels, [(Clip(1, 0.5f), 4), (Clip(1, 0.25f), 5)], gapFrames: 1);
        var buf = Read(s, 6);

        Assert.Equal(0.5f, buf[4 * Channels + 4]); // cycle = 4 frames, so frame 4 starts again
    }

    [Fact]
    public void OtherChannelsStaySilent()
    {
        var s = new ClipSequence(Channels, [(Clip(4, 0.5f), 6)], gapFrames: 0);
        var buf = Read(s, 4);

        for (var ch = 0; ch < Channels; ch++)
            if (ch != 6) Assert.All(Enumerable.Range(0, 4), f => Assert.Equal(0f, buf[f * Channels + ch]));
    }
}
