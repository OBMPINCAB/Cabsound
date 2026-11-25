using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class ProgramVolumesTests
{
    [Theory]
    [InlineData("VPinballX64")]
    [InlineData("VPinballX_GL64")]
    [InlineData("vpinballx")]
    [InlineData("Future Pinball")]
    [InlineData("PinUpMenu")]
    [InlineData("PinUpPlayer")]
    [InlineData("PinUpDisplayX2")]
    public void PinballPrograms_AreChecked(string process)
    {
        Assert.True(ProgramVolumes.IsPinball(process));
    }

    [Theory]
    [InlineData("opera")]
    [InlineData("CabSound")]
    [InlineData("Idle")]
    [InlineData("")]
    public void OtherPrograms_AreLeftAlone(string process)
    {
        Assert.False(ProgramVolumes.IsPinball(process));
    }

    [Fact]
    public void ToFix_TurnedDownPinballProgram()
    {
        var vpx = new ProgramVolume("VPinballX64", 0, Muted: false);
        Assert.Equal([vpx], ProgramVolumes.ToFix([vpx]));
    }

    [Fact]
    public void ToFix_MutedPinballProgramAtFullVolume()
    {
        var popper = new ProgramVolume("PinUpMenu", 100, Muted: true);
        Assert.Equal([popper], ProgramVolumes.ToFix([popper]));
    }

    [Fact]
    public void ToFix_Nothing_WhenAllAtFullVolume()
    {
        Assert.Empty(ProgramVolumes.ToFix([new("VPinballX64", 100, false), new("PinUpMenu", 100, false)]));
    }

    [Fact]
    public void ToFix_IgnoresOtherPrograms()
    {
        Assert.Empty(ProgramVolumes.ToFix([new("opera", 10, true)]));
    }

    [Fact]
    public void ToFix_KeepsOnlyTheProgramsThatNeedIt()
    {
        var vpx = new ProgramVolume("VPinballX64", 40, false);
        var result = ProgramVolumes.ToFix([new("PinUpMenu", 100, false), vpx, new("opera", 0, false)]);
        Assert.Equal([vpx], result);
    }

    [Fact]
    public void Message_NamesTheProgramAndWhatWasWrong()
    {
        var text = ProgramVolumes.Message([new("VPinballX64", 0, false)]);
        Assert.Contains("VPinballX64", text);
        Assert.Contains("0%", text);
        Assert.Contains("100%", text);
    }

    [Fact]
    public void Message_SaysMuted()
    {
        Assert.Contains("muted", ProgramVolumes.Message([new("PinUpMenu", 100, true)]));
    }

    [Fact]
    public void Message_ListsEveryProgram()
    {
        var text = ProgramVolumes.Message([new("VPinballX64", 0, false), new("PinUpPlayer", 50, true)]);
        Assert.Contains("VPinballX64", text);
        Assert.Contains("PinUpPlayer", text);
    }
}
