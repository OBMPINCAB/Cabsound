namespace CabSound.Core;

public sealed record ImportResult(GroupLevels Calibration, List<TableLevel> Tables);

/// <summary>
/// Turns PinVol's rows into CabSound's model: the "System" row becomes the cabinet calibration
/// and every table keeps only its difference from it. Totals (calibration + offset) stay exactly
/// as in PinVol, even where an offset ends up beyond the SSF limit.
/// </summary>
public static class PinVolImport
{
    public const string SystemRow = "System";

    public static ImportResult FromPinVol(IEnumerable<TableLevel> rows)
    {
        var list = rows.ToList();
        var system = list.FirstOrDefault(t => t.Name == SystemRow);
        var c = system is null
            ? new GroupLevels(0, 0, 0, 0)
            : new GroupLevels(system.BackglassDb, system.RearDb, system.FrontDb, 0);

        var tables = list.Select(t => t with
        {
            BackglassDb = t.BackglassDb - c.Backglass,
            RearDb = t.RearDb - c.Rear,
            FrontDb = t.FrontDb - c.Front,
        }).ToList();

        return new ImportResult(c, tables);
    }
}
