using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class SsfIncludeFileTests
{
    [Fact]
    public void Build_MatchesPinVolOutputExceptHeader()
    {
        var pinVolOutput = File.ReadAllText(Path.Combine("TestData", "PinVolSSF.txt"));
        var level = new TableLevel("System", 100, 66, -10, 10, 10);

        var ours = SsfIncludeFile.Build(level);

        Assert.Equal(DropFirstLine(pinVolOutput), DropFirstLine(ours));
    }

    [Fact]
    public void Build_MapsBackglassRearFrontToCorrectChannels()
    {
        var text = SsfIncludeFile.Build(new TableLevel("VP.X", 100, 66, -3, 4, 5));

        Assert.Contains("Channel: L R\r\nPreamp: -3 DB\r\n", text);
        Assert.Contains("Channel: RL RR\r\nPreamp: 4 DB\r\n", text);
        Assert.Contains("Channel: SL SR\r\nPreamp: 5 DB\r\n", text);
    }

    [Fact]
    public void Build_StartsWithDoNotEditHeaderAndPostMixStage()
    {
        var lines = SsfIncludeFile.Build(new TableLevel("VP.X", 100, 66, 0, 0, 0)).Split("\r\n");

        Assert.StartsWith("#", lines[0]);
        Assert.Contains("do not edit", lines[0]);
        Assert.Equal("Stage: post-mix", lines[1]);
    }

    private static string DropFirstLine(string s) => s[(s.IndexOf('\n') + 1)..];
}
