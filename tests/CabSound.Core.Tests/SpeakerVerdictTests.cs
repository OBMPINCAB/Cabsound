using CabSound.Core;
using Xunit;
using static CabSound.Core.Speaker;

namespace CabSound.Core.Tests;

public class SpeakerVerdictTests
{
    /// <summary>Everything heard at its own place, except the given answers.</summary>
    static Dictionary<Speaker, Speaker?> Heard(params (Speaker Played, Speaker? At)[] answers)
    {
        var heard = Speakers.All.ToDictionary(s => s, s => (Speaker?)s);
        foreach (var (played, at) in answers) heard[played] = at;
        return heard;
    }

    static readonly (Speaker, Speaker?)[] FrontRear =
        [(FrontLeft, RearLeft), (RearLeft, FrontLeft), (FrontRight, RearRight), (RearRight, FrontRight)];

    [Theory]
    [InlineData(BackboxLeft, 0, "Backbox left")]
    [InlineData(BackboxRight, 1, "Backbox right")]
    [InlineData(FrontLeft, 4, "Front left")]
    [InlineData(FrontRight, 5, "Front right")]
    [InlineData(RearLeft, 6, "Rear left")]
    [InlineData(RearRight, 7, "Rear right")]
    public void Speakers_ChannelAndName(Speaker speaker, int channel, string name)
    {
        Assert.Equal(channel, Speakers.Channel(speaker));
        Assert.Equal(name, Speakers.Name(speaker));
    }

    [Fact]
    public void Speakers_All_IsTheSixInOrder()
    {
        Assert.Equal([BackboxLeft, BackboxRight, FrontLeft, FrontRight, RearLeft, RearRight], Speakers.All);
    }

    [Fact]
    public void AllCorrect()
    {
        var v = SpeakerVerdict.From(Heard());

        Assert.Equal(VerdictKind.AllCorrect, v.Kind);
        Assert.Equal("all correct", v.Summary);
        Assert.Equal("Every speaker plays where it should.", v.Advice);
    }

    [Fact]
    public void NotHeard_NamesTheSpeakers()
    {
        var v = SpeakerVerdict.From(Heard((FrontLeft, null), (RearRight, null)));

        Assert.Equal(VerdictKind.NotHeard, v.Kind);
        Assert.Equal("not heard: Front left, Rear right", v.Summary);
        Assert.Equal("Check the cable and the amp channel of each speaker listed as not heard.", v.Advice);
    }

    [Fact]
    public void FrontRearExchanged_WhenAllFourExcitersAreAtTheOtherEnd()
    {
        var v = SpeakerVerdict.From(Heard(FrontRear));

        Assert.Equal(VerdictKind.FrontRearExchanged, v.Kind);
        Assert.Equal("front and rear exciters are exchanged", v.Summary);
        Assert.Equal("The Side and Rear jacks are the other way round. CabSound can switch its jack swap setting for you.", v.Advice);
    }

    [Fact]
    public void LeftRightCrossed_NamesThePair()
    {
        var v = SpeakerVerdict.From(Heard((FrontLeft, FrontRight), (FrontRight, FrontLeft)));

        Assert.Equal(VerdictKind.LeftRightCrossed, v.Kind);
        Assert.Equal("left and right are crossed: front exciters", v.Summary);
        Assert.Equal("Two plugs are exchanged on the amp or the sound card for each pair named.", v.Advice);
    }

    [Fact]
    public void LeftRightCrossed_TwoPairs_NamesBoth()
    {
        var v = SpeakerVerdict.From(Heard((BackboxLeft, BackboxRight), (BackboxRight, BackboxLeft), (RearLeft, RearRight), (RearRight, RearLeft)));

        Assert.Equal(VerdictKind.LeftRightCrossed, v.Kind);
        Assert.Equal("left and right are crossed: backbox, rear exciters", v.Summary);
    }

    [Fact]
    public void Other_WhenTwoSpeakersAreHeardAtTheSamePlace()
    {
        var v = SpeakerVerdict.From(Heard((FrontRight, FrontLeft)));

        Assert.Equal(VerdictKind.Other, v.Kind);
        Assert.Equal("speakers are not where expected", v.Summary);
        Assert.Equal("Run the check again. If it comes out the same, show this list to Claude.", v.Advice);
    }

    [Fact]
    public void NotHeard_WinsOverTheExchangePattern()
    {
        var v = SpeakerVerdict.From(Heard([.. FrontRear[..3], (RearRight, null)]));

        Assert.Equal(VerdictKind.NotHeard, v.Kind);
        Assert.Equal("not heard: Rear right", v.Summary);
    }

    [Fact]
    public void Other_WhenExcitersAreExchangedAndTheBackboxIsCrossed()
    {
        var v = SpeakerVerdict.From(Heard([.. FrontRear, (BackboxLeft, BackboxRight), (BackboxRight, BackboxLeft)]));

        Assert.Equal(VerdictKind.Other, v.Kind);
    }

    [Fact]
    public void Other_WhenOnlyOneSideIsExchangedFrontToRear()
    {
        Assert.Equal(VerdictKind.Other, SpeakerVerdict.From(Heard((FrontLeft, RearLeft), (RearLeft, FrontLeft))).Kind);
    }

    [Fact]
    public void Lines_OnePerSpeakerInOrder()
    {
        var v = SpeakerVerdict.From(Heard((FrontLeft, RearLeft), (RearRight, null)));

        Assert.Equal(
        [
            "Backbox left: correct", "Backbox right: correct", "Front left: heard at Rear left",
            "Front right: correct", "Rear left: correct", "Rear right: not heard",
        ], v.Lines);
    }

    [Fact]
    public void MissingSpeaker_Throws()
    {
        var heard = Heard();
        heard.Remove(RearLeft);

        Assert.Throws<ArgumentException>(() => SpeakerVerdict.From(heard));
    }
}
