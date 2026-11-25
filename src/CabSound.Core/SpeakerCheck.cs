namespace CabSound.Core;

public enum SpeakerStep { Prepare, Listen, Result }

/// <summary>What the owner said about the speaker that is playing.</summary>
public enum SpeakerMark { None, Right, Wrong, NotHeard }

/// <summary>
/// The speaker check's steps: the test sound plays on one named speaker at a time, in a fixed order
/// (<see cref="Speakers.All"/>), until the owner moves on. He marks it right place, wrong place (and then
/// where he heard it) or not heard; nothing moves by itself. After the sixth the verdict is ready
/// (<see cref="SpeakerVerdict"/>). The groups play at their calibrated balance. No Windows code, no timers.
/// </summary>
public sealed class SpeakerCheck
{
    private readonly Dictionary<Speaker, (SpeakerMark Mark, Speaker? Place)> _answers = [];
    private int _index;

    public SpeakerCheck(GroupLevels calibration)
    {
        var highest = Math.Max(calibration.Backglass, Math.Max(calibration.Rear, calibration.Front));
        Trial = new GroupLevels(calibration.Backglass - highest, calibration.Rear - highest, calibration.Front - highest, 0);
    }

    public SpeakerStep Step { get; private set; }

    /// <summary>Which speaker of the six is playing (1..6); valid while listening.</summary>
    public int Number => _index + 1;

    /// <summary>The speaker playing; valid while listening.</summary>
    public Speaker Current => Speakers.All[_index];

    /// <summary>The calibration with its highest group at 0: what a table at 0 / 0 / 0 gets, before the headroom.</summary>
    public GroupLevels Trial { get; }

    /// <summary>The mark of the speaker playing; None outside the Listen step.</summary>
    public SpeakerMark Mark => Step == SpeakerStep.Listen ? Answer.Mark : SpeakerMark.None;

    /// <summary>Where the speaker playing was heard instead; null unless it is marked wrong and a place was picked.</summary>
    public Speaker? WrongPlace => Mark == SpeakerMark.Wrong ? Answer.Place : null;

    /// <summary>Next does something: always on Prepare; while listening only with a complete answer.</summary>
    public bool CanGoNext => Step switch
    {
        SpeakerStep.Prepare => true,
        SpeakerStep.Listen => Mark is SpeakerMark.Right or SpeakerMark.NotHeard || WrongPlace is not null,
        _ => false,
    };

    /// <summary>Stop was pressed on this speaker (silence until Play or another speaker).</summary>
    public bool Stopped { get; private set; }

    /// <summary>The card channels that sound now: both backbox speakers to set the volume, then one speaker; null = silence.</summary>
    public int[]? PlayingOn => Step switch
    {
        SpeakerStep.Prepare => [Speakers.Channel(Speaker.BackboxLeft), Speakers.Channel(Speaker.BackboxRight)],
        SpeakerStep.Listen when !Stopped => [Speakers.Channel(Current)],
        _ => null,
    };

    /// <summary>Non-null on Result only.</summary>
    public SpeakerVerdict? Verdict => Step == SpeakerStep.Result
        ? SpeakerVerdict.From(Speakers.All.ToDictionary(s => s, s => _answers[s].Mark == SpeakerMark.Right ? s : _answers[s].Place))
        : null;

    public void MarkRight() => Set(SpeakerMark.Right, null);

    public void MarkNotHeard() => Set(SpeakerMark.NotHeard, null);

    /// <summary>Wrong place; a place already picked for this speaker is kept.</summary>
    public void MarkWrong() => Set(SpeakerMark.Wrong, WrongPlace);

    /// <summary>Wrong place, and where it was heard. The speaker's own place is not an answer here.</summary>
    public void HeardAt(Speaker place)
    {
        if (Step == SpeakerStep.Listen && place != Current) Set(SpeakerMark.Wrong, place);
    }

    public void Stop()
    {
        if (Step == SpeakerStep.Listen) Stopped = true;
    }

    public void Play() => Stopped = false;

    /// <summary>Prepare → the first speaker (marks kept). Listening → the next speaker or the result, once the answer is complete.</summary>
    public void Next()
    {
        if (!CanGoNext) return;
        if (Step == SpeakerStep.Prepare) Go(SpeakerStep.Listen, 0);
        else if (_index < Speakers.All.Count - 1) Go(SpeakerStep.Listen, _index + 1);
        else Go(SpeakerStep.Result, _index);
    }

    /// <summary>The previous speaker with its mark; at the first speaker, back to Prepare.</summary>
    public void Back()
    {
        if (Step != SpeakerStep.Listen) return;
        if (_index == 0) Go(SpeakerStep.Prepare, 0);
        else Go(SpeakerStep.Listen, _index - 1);
    }

    /// <summary>From the result: a new run, all marks gone.</summary>
    public void Again()
    {
        if (Step != SpeakerStep.Result) return;
        _answers.Clear();
        Go(SpeakerStep.Listen, 0);
    }

    private (SpeakerMark Mark, Speaker? Place) Answer => _answers.GetValueOrDefault(Current);

    private void Set(SpeakerMark mark, Speaker? place)
    {
        if (Step == SpeakerStep.Listen) _answers[Current] = (mark, place);
    }

    private void Go(SpeakerStep step, int index)
    {
        Step = step;
        _index = index;
        Stopped = false;
    }
}
