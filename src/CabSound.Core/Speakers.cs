namespace CabSound.Core;

/// <summary>The cabinet's six speakers. Front = the exciters near the player, Rear = near the backbox.</summary>
public enum Speaker { BackboxLeft, BackboxRight, FrontLeft, FrontRight, RearLeft, RearRight }

public static class Speakers
{
    public static IReadOnlyList<Speaker> All { get; } = Enum.GetValues<Speaker>();

    /// <summary>The card channel (7.1 order) CabSound plays this speaker on: front exciters = RL RR, rear = SL SR.</summary>
    public static int Channel(Speaker s) => s switch
    {
        Speaker.BackboxLeft => 0,
        Speaker.BackboxRight => 1,
        Speaker.FrontLeft => 4,
        Speaker.FrontRight => 5,
        Speaker.RearLeft => 6,
        _ => 7,
    };

    public static string Name(Speaker s) => s switch
    {
        Speaker.BackboxLeft => "Backbox left",
        Speaker.BackboxRight => "Backbox right",
        Speaker.FrontLeft => "Front left",
        Speaker.FrontRight => "Front right",
        Speaker.RearLeft => "Rear left",
        _ => "Rear right",
    };
}
