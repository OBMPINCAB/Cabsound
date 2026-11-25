using System.Text;

namespace CabSound.Core;

/// <summary>Reads the table script from a .vpx GameStg/GameData stream (records: int length, 4-byte tag, data).</summary>
public static class VpxGameData
{
    /// <summary>The script (the "CODE" record: tag, then int length + bytes), read as Latin-1 so no byte is lost; null if none.</summary>
    public static string? ReadScript(byte[] gameData)
    {
        var p = 0;
        while (p + 8 <= gameData.Length)
        {
            var length = BitConverter.ToInt32(gameData, p);
            var tag = Encoding.ASCII.GetString(gameData, p + 4, 4);
            if (tag == "ENDB") return null;
            if (tag == "CODE")
            {
                if (p + 12 > gameData.Length) return null;
                var n = BitConverter.ToInt32(gameData, p + 8);
                return p + 12 + n <= gameData.Length ? Encoding.Latin1.GetString(gameData, p + 12, n) : null;
            }
            if (length < 4) return null;
            p += 4 + length;
        }
        return null;
    }
}
