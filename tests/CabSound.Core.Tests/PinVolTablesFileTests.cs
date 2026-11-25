using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class PinVolTablesFileTests
{
    private static string RealFile => File.ReadAllText(Path.Combine("TestData", "PinVolTables.ini"));

    [Fact]
    public void Parse_ReadsAllRowsFromRealFile()
    {
        var tables = PinVolTablesFile.Parse(RealFile);

        Assert.Equal(171, tables.Count);
    }

    [Fact]
    public void Parse_ReadsAllSixColumns()
    {
        var text = "# header\r\n\r\nVP.Attack from Mars (Bally 1995) LW 2.02\t100\t66\t-9\t10\t10\r\n";

        var t = Assert.Single(PinVolTablesFile.Parse(text));

        Assert.Equal("VP.Attack from Mars (Bally 1995) LW 2.02", t.Name);
        Assert.Equal(100, t.Volume);
        Assert.Equal(66, t.Volume2);
        Assert.Equal(-9, t.BackglassDb);
        Assert.Equal(10, t.RearDb);
        Assert.Equal(10, t.FrontDb);
    }

    [Fact]
    public void Parse_KeepsSpecialEntries()
    {
        var tables = PinVolTablesFile.Parse(RealFile);

        Assert.Contains(tables, t => t.Name == "System");
        Assert.Contains(tables, t => t.Name == "PinUP Popper");
        Assert.Contains(tables, t => t.Name.StartsWith("FP."));
    }

    [Fact]
    public void Parse_SkipsMalformedRowsInsteadOfCrashing()
    {
        var text = "Good\t100\t66\t0\t0\t0\r\nBad row\t100\r\nAlsoBad\tx\t66\t0\t0\t0\r\n";

        var t = Assert.Single(PinVolTablesFile.Parse(text));
        Assert.Equal("Good", t.Name);
    }

    [Fact]
    public void Format_ThenParse_RoundTripsRealFileWithoutLoss()
    {
        var original = PinVolTablesFile.Parse(RealFile);

        var again = PinVolTablesFile.Parse(PinVolTablesFile.Format(original));

        Assert.Equal(original, again);
    }

    [Fact]
    public void Format_UsesTabsAndWindowsLineEndings()
    {
        var text = PinVolTablesFile.Format(new[] { new TableLevel("VP.X", 100, 66, -1, 2, 3) });

        Assert.Contains("VP.X\t100\t66\t-1\t2\t3\r\n", text);
    }
}
