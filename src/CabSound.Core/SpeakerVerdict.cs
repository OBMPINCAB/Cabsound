using static CabSound.Core.Speaker;

namespace CabSound.Core;

public enum VerdictKind { AllCorrect, NotHeard, FrontRearExchanged, LeftRightCrossed, Other }

/// <summary>
/// What the speaker check found: where the owner heard each speaker against where it should be.
/// Summary = one line (saved and shown on the Sound tab), Advice = what to do, Lines = one per speaker.
/// Only the exact "front and rear exchanged" pattern may lead to the jack swap being switched.
/// </summary>
public sealed record SpeakerVerdict(VerdictKind Kind, string Summary, string Advice, IReadOnlyList<string> Lines)
{
    private static readonly (Speaker Left, Speaker Right, string Name)[] Pairs =
    [
        (BackboxLeft, BackboxRight, "backbox"), (FrontLeft, FrontRight, "front exciters"), (RearLeft, RearRight, "rear exciters"),
    ];

    private static readonly (Speaker, Speaker)[] FrontRear =
        [(FrontLeft, RearLeft), (RearLeft, FrontLeft), (FrontRight, RearRight), (RearRight, FrontRight)];

    /// <param name="heard">For each of the six speakers, the place it was heard; null = not heard.</param>
    public static SpeakerVerdict From(IReadOnlyDictionary<Speaker, Speaker?> heard)
    {
        if (Speakers.All.Any(s => !heard.ContainsKey(s))) throw new ArgumentException("An answer is needed for every speaker", nameof(heard));

        var lines = Speakers.All.Select(s => $"{Speakers.Name(s)}: " + (heard[s] is not { } at ? "not heard"
            : at == s ? "correct" : $"heard at {Speakers.Name(at)}")).ToList();
        SpeakerVerdict Verdict(VerdictKind kind, string summary, string advice) => new(kind, summary, advice, lines);

        var missing = Speakers.All.Where(s => heard[s] is null).ToList();
        if (missing.Count > 0)
            return Verdict(VerdictKind.NotHeard, "not heard: " + string.Join(", ", missing.Select(Speakers.Name)),
                "Check the cable and the amp channel of each speaker listed as not heard.");

        if (Speakers.All.All(s => heard[s] == s))
            return Verdict(VerdictKind.AllCorrect, "all correct", "Every speaker plays where it should.");

        if (heard[BackboxLeft] == BackboxLeft && heard[BackboxRight] == BackboxRight && FrontRear.All(p => heard[p.Item1] == p.Item2))
            return Verdict(VerdictKind.FrontRearExchanged, "front and rear exciters are exchanged",
                "The Side and Rear jacks are the other way round. CabSound can switch its jack swap setting for you.");

        bool Correct((Speaker Left, Speaker Right, string) p) => heard[p.Left] == p.Left && heard[p.Right] == p.Right;
        bool Crossed((Speaker Left, Speaker Right, string) p) => heard[p.Left] == p.Right && heard[p.Right] == p.Left;
        if (Pairs.All(p => Correct(p) || Crossed(p)))
            return Verdict(VerdictKind.LeftRightCrossed,
                "left and right are crossed: " + string.Join(", ", Pairs.Where(Crossed).Select(p => p.Name)),
                "Two plugs are exchanged on the amp or the sound card for each pair named.");

        return Verdict(VerdictKind.Other, "speakers are not where expected",
            "Run the check again. If it comes out the same, show this list to Claude.");
    }
}
