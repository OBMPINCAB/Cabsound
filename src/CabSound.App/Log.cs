using System;
using System.IO;

namespace CabSound.App;

/// <summary>CabSound.log next to the exe (rotated when over 2 MB). Never throws.</summary>
public static class Log
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly object Gate = new();
    private static string? _path;

    public static void Init(string dir)
    {
        _path = Path.Combine(dir, "CabSound.log");
        try
        {
            if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                File.Move(_path, _path + ".old", overwrite: true);
        }
        catch { /* logging must never break the app */ }
    }

    public static void Write(string message)
    {
        if (_path is null) return;
        try
        {
            lock (Gate) File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}\r\n");
        }
        catch { /* logging must never break the app */ }
    }
}
