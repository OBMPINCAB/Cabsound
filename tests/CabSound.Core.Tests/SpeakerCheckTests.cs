using CabSound.Core;
using Xunit;
using static CabSound.Core.Speaker;

namespace CabSound.Core.Tests;

public class SpeakerCheckTests
{
    static readonly GroupLevels Calibration = new(Backglass: -12, Rear: 10, Front: 10, Sub: 0);

    static SpeakerCheck Check() => new(Calibration);

    /// <summary>A check on the Listen step.</summary>
    static SpeakerCheck Listening()
    {
        var check = Check();
        check.Next();
        return check;
    }

    /// <summary>Marks the speaker playing and every one after it "right place", up to the result.</summary>
    static void AllRight(SpeakerCheck check)
    {
        while (check.Step == SpeakerStep.Listen)
        {
            check.MarkRight();
            check.Next();
        }
    }

    [Fact]
    public void StartsAtPrepare_PlayingBothBackboxSpeakers()
    {
        var check = Check();

        Assert.Equal(SpeakerStep.Prepare, check.Step);
        Assert.Equal([0, 1], check.PlayingOn!);
        Assert.Null(check.Verdict);
    }

    [Fact]
    public void Trial_IsTheCalibrationWithItsHighestGroupAtZero()
    {
        Assert.Equal(new GroupLevels(Backglass: -22, Rear: 0, Front: 0, Sub: 0), Check().Trial);
    }

    [Fact]
    public void Next_StartsWithBackboxLeft_Unmarked()
    {
        var check = Listening();

        Assert.Equal(SpeakerStep.Listen, check.Step);
        Assert.Equal(BackboxLeft, check.Current);
        Assert.Equal(1, check.Number);
        Assert.Equal(SpeakerMark.None, check.Mark);
        Assert.False(check.CanGoNext);
        Assert.Equal([Speakers.Channel(BackboxLeft)], check.PlayingOn!);
    }

    [Fact]
    public void Next_WithoutAMark_StaysOnTheSpeaker()
    {
        var check = Listening();

        check.Next();

        Assert.Equal(BackboxLeft, check.Current);
    }

    [Fact]
    public void Marking_DoesNotMoveOn_NextDoes()
    {
        var check = Listening();

        check.MarkRight();
        Assert.Equal(BackboxLeft, check.Current);
        Assert.True(check.CanGoNext);

        check.Next();
        Assert.Equal(BackboxRight, check.Current);
        Assert.Equal(2, check.Number);
        Assert.Equal(SpeakerMark.None, check.Mark);
    }

    [Fact]
    public void OrderIsFixed()
    {
        var check = Listening();
        var seen = new List<Speaker>();

        while (check.Step == SpeakerStep.Listen)
        {
            seen.Add(check.Current);
            check.MarkRight();
            check.Next();
        }

        Assert.Equal(Speakers.All, seen);
        Assert.Equal(SpeakerStep.Result, check.Step);
    }

    [Fact]
    public void WrongPlace_NeedsAPlace_AndNotItsOwn()
    {
        var check = Listening();

        check.MarkWrong();
        Assert.Equal(SpeakerMark.Wrong, check.Mark);
        Assert.False(check.CanGoNext);

        check.HeardAt(BackboxLeft);
        Assert.Null(check.WrongPlace);
        Assert.False(check.CanGoNext);

        check.HeardAt(RearLeft);
        Assert.Equal(RearLeft, check.WrongPlace);
        Assert.True(check.CanGoNext);
    }

    [Fact]
    public void HeardAt_AlsoMarksWrong()
    {
        var check = Listening();

        check.HeardAt(RearLeft);

        Assert.Equal(SpeakerMark.Wrong, check.Mark);
    }

    [Fact]
    public void AllRight_IsAllCorrect()
    {
        var check = Listening();

        AllRight(check);

        Assert.Equal(VerdictKind.AllCorrect, check.Verdict!.Kind);
    }

    [Fact]
    public void ExcitersHeardAtTheOtherEnd_IsFrontRearExchanged()
    {
        var check = Listening();
        check.MarkRight(); check.Next();
        check.MarkRight(); check.Next();

        check.HeardAt(RearLeft); check.Next();
        check.HeardAt(RearRight); check.Next();
        check.HeardAt(FrontLeft); check.Next();
        check.HeardAt(FrontRight); check.Next();

        Assert.Equal(VerdictKind.FrontRearExchanged, check.Verdict!.Kind);
    }

    [Fact]
    public void NotHeard_ReachesTheVerdict()
    {
        var check = Listening();

        check.MarkNotHeard();
        Assert.True(check.CanGoNext);
        check.Next();
        AllRight(check);

        Assert.Equal(VerdictKind.NotHeard, check.Verdict!.Kind);
    }

    [Fact]
    public void WrongThenRight_ForgetsThePlace()
    {
        var check = Listening();

        check.HeardAt(RearLeft);
        check.MarkRight();
        Assert.Null(check.WrongPlace);
        AllRight(check);

        Assert.Equal(VerdictKind.AllCorrect, check.Verdict!.Kind);
    }

    [Fact]
    public void MarkWrongAgain_KeepsThePlace()
    {
        var check = Listening();

        check.HeardAt(RearLeft);
        check.MarkWrong();

        Assert.Equal(RearLeft, check.WrongPlace);
    }

    [Fact]
    public void Stop_Silences_PlayRestarts_NextPlaysTheNextSpeaker()
    {
        var check = Listening();

        check.Stop();
        Assert.True(check.Stopped);
        Assert.Null(check.PlayingOn!);

        check.Play();
        Assert.Equal([Speakers.Channel(BackboxLeft)], check.PlayingOn!);

        check.Stop();
        check.MarkRight();
        check.Next();
        Assert.False(check.Stopped);
        Assert.Equal([Speakers.Channel(BackboxRight)], check.PlayingOn!);
    }

    [Fact]
    public void Back_ShowsTheEarlierSpeakerWithItsMark()
    {
        var check = Listening();
        check.HeardAt(RearLeft);
        check.Next();

        check.Back();

        Assert.Equal(BackboxLeft, check.Current);
        Assert.Equal(SpeakerMark.Wrong, check.Mark);
        Assert.Equal(RearLeft, check.WrongPlace);
        Assert.True(check.CanGoNext);
    }

    [Fact]
    public void Back_AfterStop_PlaysTheEarlierSpeaker()
    {
        var check = Listening();
        check.MarkRight();
        check.Next();
        check.Stop();

        check.Back();

        Assert.Equal([Speakers.Channel(BackboxLeft)], check.PlayingOn!);
    }

    [Fact]
    public void Back_AtTheFirstSpeaker_ReturnsToPrepare_NextKeepsTheMarks()
    {
        var check = Listening();
        check.MarkNotHeard();

        check.Back();
        Assert.Equal(SpeakerStep.Prepare, check.Step);
        Assert.Equal([0, 1], check.PlayingOn!);
        Assert.True(check.CanGoNext);

        check.Next();
        Assert.Equal(BackboxLeft, check.Current);
        Assert.Equal(SpeakerMark.NotHeard, check.Mark);
    }

    [Fact]
    public void Marks_OutsideListen_DoNothing()
    {
        var check = Check();

        check.MarkRight();
        check.MarkWrong();
        check.MarkNotHeard();
        check.HeardAt(RearLeft);
        check.Stop();

        Assert.Equal(SpeakerStep.Prepare, check.Step);
        Assert.Equal(SpeakerMark.None, check.Mark);
        Assert.NotNull(check.PlayingOn!);
        check.Next();
        Assert.Equal(SpeakerMark.None, check.Mark);
    }

    [Fact]
    public void Result_IsSilent_NextAndBackDoNothing()
    {
        var check = Listening();
        AllRight(check);

        check.Next();
        check.Back();
        check.MarkNotHeard();

        Assert.Equal(SpeakerStep.Result, check.Step);
        Assert.Null(check.PlayingOn!);
        Assert.False(check.CanGoNext);
        Assert.Equal(VerdictKind.AllCorrect, check.Verdict!.Kind);
    }

    [Fact]
    public void Again_StartsANewRun_MarksGone()
    {
        var check = Listening();
        AllRight(check);

        check.Again();

        Assert.Equal(SpeakerStep.Listen, check.Step);
        Assert.Equal(BackboxLeft, check.Current);
        Assert.Equal(SpeakerMark.None, check.Mark);
        Assert.Null(check.Verdict);
    }

    [Fact]
    public void Again_BeforeTheResult_DoesNothing()
    {
        var check = Listening();
        check.MarkRight();
        check.Next();

        check.Again();

        Assert.Equal(BackboxRight, check.Current);
    }
}
