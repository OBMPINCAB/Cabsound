using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class PinVolImportTests
{
    private static string RealFile => File.ReadAllText(Path.Combine("TestData", "PinVolTables.ini"));

    [Fact]
    public void SystemRow_BecomesTheCalibration()
    {
        var result = PinVolImport.FromPinVol(new[] { new TableLevel("System", 100, 66, -10, 10, 8) });

        Assert.Equal(new GroupLevels(-10, 10, 8, 0), result.Calibration);
    }

    [Fact]
    public void Tables_BecomeOffsetsFromSystem()
    {
        var result = PinVolImport.FromPinVol(new[]
        {
            new TableLevel("System", 100, 66, -10, 10, 10),
            new TableLevel("VP.A", 43, 71, -10, 10, 10),
            new TableLevel("VP.B", 90, 66, -8, 5, 0),
        });

        Assert.Equal(new TableLevel("VP.A", 43, 71, 0, 0, 0), result.Tables.Single(t => t.Name == "VP.A"));
        Assert.Equal(new TableLevel("VP.B", 90, 66, 2, -5, -10), result.Tables.Single(t => t.Name == "VP.B"));
    }

    [Fact]
    public void SystemRow_IsKeptWithZeroOffsets_SoNewTablesStillGetItsVolume()
    {
        var result = PinVolImport.FromPinVol(new[] { new TableLevel("System", 100, 66, -10, 10, 10) });

        Assert.Equal(new TableLevel("System", 100, 66, 0, 0, 0), Assert.Single(result.Tables));
    }

    [Fact]
    public void NoSystemRow_MeansNoCalibration()
    {
        var result = PinVolImport.FromPinVol(new[] { new TableLevel("VP.A", 43, 71, -3, 2, 1) });

        Assert.Equal(new GroupLevels(0, 0, 0, 0), result.Calibration);
        Assert.Equal(new TableLevel("VP.A", 43, 71, -3, 2, 1), Assert.Single(result.Tables));
    }

    [Fact]
    public void OffsetsBeyondTheLimit_AreKeptExact_SoTablesSoundTheSame()
    {
        var result = PinVolImport.FromPinVol(PinVolTablesFile.Parse(RealFile));

        var totan = result.Tables.Single(t => t.Name == "VP.Tales of the Arabian Nights (Williams 1996) LW");
        Assert.Equal(20, totan.BackglassDb);
    }

    [Fact]
    public void RealFile_TotalLevelsAreUnchanged()
    {
        var original = PinVolTablesFile.Parse(RealFile);
        var result = PinVolImport.FromPinVol(original);
        var c = result.Calibration;

        Assert.Equal(original.Count, result.Tables.Count);
        foreach (var (before, after) in original.Zip(result.Tables))
        {
            Assert.Equal(before.Name, after.Name);
            Assert.Equal(before.BackglassDb, after.BackglassDb + c.Backglass);
            Assert.Equal(before.RearDb, after.RearDb + c.Rear);
            Assert.Equal(before.FrontDb, after.FrontDb + c.Front);
        }
    }

    [Fact]
    public void RealFile_TheCommonRowBecomesZero()
    {
        var result = PinVolImport.FromPinVol(PinVolTablesFile.Parse(RealFile));

        // 27 rows (System included) were at PinVol's -10 / +10 / +10
        Assert.Equal(27, result.Tables.Count(t => t is { BackglassDb: 0, RearDb: 0, FrontDb: 0 }));
    }
}
