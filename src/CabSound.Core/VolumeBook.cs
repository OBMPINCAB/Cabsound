namespace CabSound.Core;

public enum SsfGroup { Backglass, Rear, Front, Sub }

/// <summary>
/// Per-table volume and SSF offsets (on top of the cabinet calibration), keyed like PinVol
/// ("VP.Table Name"). New tables get the "System" entry's volume and 0 dB offsets.
/// Offsets range from -cutLimit to +boostLimit. Offsets imported beyond that are kept; buttons
/// can only move them back toward it.
/// </summary>
public sealed class VolumeBook
{
    private readonly List<TableLevel> _tables;
    private readonly int _cutLimitDb;
    private readonly int _boostLimitDb;

    public VolumeBook(IEnumerable<TableLevel> tables, int cutLimitDb, int boostLimitDb)
    {
        _tables = tables.ToList();
        _cutLimitDb = cutLimitDb;
        _boostLimitDb = boostLimitDb;
    }

    public TableLevel Get(string key)
    {
        if (Find(key) is { } t) return t;
        var system = Find(PinVolImport.SystemRow);
        return new TableLevel(key, system?.Volume ?? 100, system?.Volume2 ?? 100, 0, 0, 0);
    }

    public TableLevel ChangeVolume(string key, int delta)
    {
        var t = Get(key);
        return Save(t with { Volume = Math.Clamp(t.Volume + delta, 0, 100) });
    }

    public TableLevel ChangeSsf(string key, SsfGroup group, int deltaDb)
    {
        var t = Get(key);
        int Clamp(int v)
        {
            var next = v + deltaDb;
            if (InRange(next)) return next;
            // Beyond the range: allow moves back toward it, otherwise stop at the edge (or stay put)
            if (!InRange(v)) return Math.Abs(next) < Math.Abs(v) ? next : v;
            return Math.Clamp(next, -_cutLimitDb, _boostLimitDb);
        }

        return Save(group switch
        {
            SsfGroup.Backglass => t with { BackglassDb = Clamp(t.BackglassDb) },
            SsfGroup.Rear => t with { RearDb = Clamp(t.RearDb) },
            SsfGroup.Front => t with { FrontDb = Clamp(t.FrontDb) },
            _ => t with { SubDb = Clamp(t.SubDb) },
        });
    }

    public bool IsAtSsfLimit(string key)
    {
        var t = Get(key);
        return new[] { t.BackglassDb, t.RearDb, t.FrontDb, t.SubDb }.Any(v => v <= -_cutLimitDb || v >= _boostLimitDb);
    }

    private bool InRange(int v) => v >= -_cutLimitDb && v <= _boostLimitDb;

    /// <summary>Stores a table's levels as given (window edits; no limits applied).</summary>
    public TableLevel Set(TableLevel t) => Save(t);

    public List<TableLevel> All() => _tables.ToList();

    /// <summary>Forgets a table (undo of a table that didn't exist before).</summary>
    public void Remove(string key) => _tables.RemoveAll(t => t.Name == key);

    /// <summary>The saved row, or null when the table has none yet.</summary>
    public TableLevel? Find(string key) => _tables.FirstOrDefault(t => t.Name == key);

    private TableLevel Save(TableLevel t)
    {
        var i = _tables.FindIndex(x => x.Name == t.Name);
        if (i >= 0) _tables[i] = t; else _tables.Add(t);
        return t;
    }
}
