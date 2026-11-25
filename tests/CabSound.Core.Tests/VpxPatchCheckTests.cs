using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class VpxPatchCheckTests
{
    private static Dictionary<string, string> H(params (string, string)[] x) => x.ToDictionary(p => p.Item1, p => p.Item2);

    [Fact]
    public void OnlyIntendedStreamsChanged_NoProblems()
    {
        var before = H(("GameStg/GameData", "a"), ("GameStg/Sound1", "b"), ("GameStg/Sound2", "c"));
        var after = H(("GameStg/GameData", "a"), ("GameStg/Sound1", "B"), ("GameStg/Sound2", "c"));
        Assert.Empty(VpxPatchCheck.Problems(before, after, ["GameStg/Sound1"]));
    }

    [Fact]
    public void OtherStreamChanged_AddedRemovedOrIntendedUnchanged_AllReported()
    {
        var before = H(("GameStg/GameData", "a"), ("GameStg/Sound1", "b"), ("GameStg/Sound3", "d"));
        var after = H(("GameStg/GameData", "X"), ("GameStg/Sound1", "b"), ("GameStg/Sound9", "e"));

        Assert.Equal(["changed: GameStg/GameData", "not changed: GameStg/Sound1", "removed: GameStg/Sound3", "added: GameStg/Sound9"],
            VpxPatchCheck.Problems(before, after, ["GameStg/Sound1"]));
    }
}
