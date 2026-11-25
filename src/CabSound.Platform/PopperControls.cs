using CabSound.Core;
using Microsoft.Data.Sqlite;

namespace CabSound.Platform;

/// <summary>Reads Popper's button assignments (read-only) from PUPDatabase.db.</summary>
public static class PopperControls
{
    public const string DefaultDb = @"C:\vPinball\PinUPSystem\PUPDatabase.db";

    static PopperControls() => SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());

    public static List<PopperControl> Read(string dbPath = DefaultDb)
    {
        var rows = new List<PopperControl>();
        using var db = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Descript, CntrlCodes, JoyCodes, Active FROM PinUPFunctions";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            rows.Add(new PopperControl(
                r.IsDBNull(0) ? "" : r.GetValue(0).ToString()!,
                r.IsDBNull(1) ? null : r.GetValue(1).ToString(),
                r.IsDBNull(2) ? null : r.GetValue(2).ToString(),
                !r.IsDBNull(3) && Convert.ToInt32(r.GetValue(3)) == 1));
        return rows;
    }
}
