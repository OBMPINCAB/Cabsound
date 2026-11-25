using System.Text;

namespace CabSound.Core;

/// <summary>
/// File writes that never leave a half-written file: write a temp file next to the target, then
/// rename it over the target (Equalizer APO reloads config files while they change).
/// UTF-8 without BOM, like Equalizer APO's own files.
/// </summary>
public static class SafeFileWriter
{
    private static readonly Encoding NoBom = new UTF8Encoding(false);

    /// <summary>Retries the rename for up to ~1 s while another program (Equalizer APO) is reading the file.</summary>
    public static void Write(string path, string text)
    {
        var temp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temp, text, NoBom);
        for (var attempt = 1; ; attempt++)
        {
            try { File.Move(temp, path, overwrite: true); return; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 20)
            {
                Thread.Sleep(50);
            }
            catch
            {
                try { File.Delete(temp); } catch { }
                throw;
            }
        }
    }

    /// <summary>Copies the file to backupDir as "name.yyyyMMdd-HHmmss.bak" and returns the copy's path.</summary>
    public static string Backup(string path, string backupDir, DateTime now)
    {
        Directory.CreateDirectory(backupDir);
        var stem = Path.Combine(backupDir, $"{Path.GetFileName(path)}.{now:yyyyMMdd-HHmmss}");
        var copy = stem + ".bak";
        for (var n = 2; File.Exists(copy); n++) copy = $"{stem}-{n}.bak";
        File.Copy(path, copy);
        return copy;
    }
}
