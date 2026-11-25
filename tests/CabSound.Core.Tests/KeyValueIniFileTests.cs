using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class KeyValueIniFileTests
{
    [Fact]
    public void Parse_ReadsRealSettingsHotkeys()
    {
        var s = KeyValueIniFile.Parse(File.ReadAllText(Path.Combine("TestData", "PinVolSettings.ini")));

        Assert.Equal("Ctrl+F9", s["globalVolUp"]);
        Assert.Equal("VolumeUp", s["localVolUp"]);
        Assert.Equal("Ctrl+Return", s["nightMode"]);
        Assert.Equal("F4", s["SSFBGVolUp"]);
        Assert.Equal("10", s["SSFdBLimit"]);
    }

    [Fact]
    public void Parse_KeepsCommasAndEqualsInsideValues()
    {
        var s = KeyValueIniFile.Parse("Device = {a}.{b},\"Speakers (2- USB)\",100,True\r\nX = a=b\r\n");

        Assert.Equal("{a}.{b},\"Speakers (2- USB)\",100,True", s["Device"]);
        Assert.Equal("a=b", s["X"]);
    }

    [Fact]
    public void Parse_ReadsRealVolumeFile()
    {
        var v = KeyValueIniFile.Parse(File.ReadAllText(Path.Combine("TestData", "PinVolVol.ini")));

        Assert.Equal("100", v["Global"]);
        Assert.Equal("100", v["Night"]);
        Assert.Equal("100", v["Default"]);
    }

    [Fact]
    public void Parse_IgnoresCommentsAndBlankLines()
    {
        var s = KeyValueIniFile.Parse("# c\r\n\r\nA = 1\r\n");

        Assert.Single(s);
    }
}
