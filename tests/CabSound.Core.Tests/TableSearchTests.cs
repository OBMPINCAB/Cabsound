using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class TableSearchTests
{
    private static TableLevel T(string name) => new(name, 100, 66, 0, 0, 0);

    private static readonly TableLevel[] Tables =
        [T("VP.Monster Bash (Williams 1998) LW"), T("System"), T("FP.Attack from Mars"), T("VP.attack and revenge")];

    [Fact]
    public void Filter_MatchesAnywhereIgnoringCaseAndPrefix()
    {
        Assert.Equal(["VP.Monster Bash (Williams 1998) LW"], TableSearch.Filter(Tables, "bash").Select(t => t.Name));
        Assert.Empty(TableSearch.Filter(Tables, "vp."));
        Assert.Equal(["VP.attack and revenge", "FP.Attack from Mars"], TableSearch.Filter(Tables, "ATTACK").Select(t => t.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Filter_EmptyText_ReturnsAllSorted(string text)
    {
        Assert.Equal(["VP.attack and revenge", "FP.Attack from Mars", "VP.Monster Bash (Williams 1998) LW", "System"],
            TableSearch.Filter(Tables, text).Select(t => t.Name));
    }

    [Fact]
    public void DisplayName_StripsPrefix()
    {
        Assert.Equal("Monster Bash (Williams 1998) LW", TableSearch.DisplayName("VP.Monster Bash (Williams 1998) LW"));
        Assert.Equal("Attack from Mars", TableSearch.DisplayName("FP.Attack from Mars"));
        Assert.Equal("System", TableSearch.DisplayName("System"));
    }

    [Theory]
    [InlineData("VP.Monster Bash", "VPX")]
    [InlineData("FP.Attack from Mars", "Future Pinball")]
    [InlineData("PinUP Popper", "Popper")]
    [InlineData("Visual Pinball", "System")]
    [InlineData("System", "System")]
    public void Source_ByKey(string key, string expected)
    {
        Assert.Equal(expected, TableSearch.Source(key));
    }
}
