using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class SoundOutputMoveTests
{
    private static VpxSound Sound(string name, SoundOutput output) =>
        VpxSound.Parse(VpxSoundTests.Stream(name, name + ".wav", 2, 16, [1, 0, 2, 0], output: (int)output));

    private static readonly List<(string Stream, VpxSound Sound)> Sounds =
    [
        ("Sound0", Sound("doomboom_1", SoundOutput.Backglass)),
        ("Sound1", Sound("doomboom_2", SoundOutput.Backglass)),
        ("Sound2", Sound("doomboom_3", SoundOutput.Table)),
        ("Sound3", Sound("voice_win", SoundOutput.Backglass)),
    ];

    [Fact]
    public void Plan_ToTable_MovesMatchingBackglassSoundsOnly()
    {
        var moves = SoundOutputMove.Plan(Sounds, ["doomboom*"], SoundOutput.Table);

        Assert.Equal(["Sound0", "Sound1"], moves.Select(m => m.Stream));
        Assert.All(moves, m => Assert.Equal(SoundOutput.Table, m.Sound.Output));
    }

    [Fact]
    public void Plan_ToTable_ChangesOnlyTheOutputByte()
    {
        var move = Assert.Single(SoundOutputMove.Plan(Sounds, ["doomboom_1"], SoundOutput.Table));

        Assert.Equal(VpxSoundTests.Stream("doomboom_1", "doomboom_1.wav", 2, 16, [1, 0, 2, 0], output: 0), move.Sound.Write());
    }

    [Fact]
    public void Plan_ToBackglass_MovesMatchingTableSoundsOnly()
    {
        var move = Assert.Single(SoundOutputMove.Plan(Sounds, ["doomboom*"], SoundOutput.Backglass));

        Assert.Equal(("Sound2", SoundOutput.Backglass), (move.Stream, move.Sound.Output));
    }

    [Fact]
    public void Plan_CaseInsensitive_NothingMatches_Empty()
    {
        Assert.Single(SoundOutputMove.Plan(Sounds, ["VOICE_WIN"], SoundOutput.Table));
        Assert.Empty(SoundOutputMove.Plan(Sounds, ["nope*"], SoundOutput.Table));
    }

    [Fact]
    public void Unmatched_ListsPatternsThatMatchNoSound()
    {
        Assert.Equal(["nope*", "x"], SoundOutputMove.Unmatched(Sounds, ["doomboom*", "nope*", "x", "VOICE_WIN"]));
    }
}
