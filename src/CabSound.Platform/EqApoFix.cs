using System.Diagnostics;
using CabSound.Core;
using Microsoft.Win32;

namespace CabSound.Platform;

/// <summary>
/// Turns Windows' audio enhancements back on for one card (the "{1da5d803…},5" bypass value → 0) so
/// Equalizer APO is loaded again. Needs administrator rights: runs in the elevated helper start of
/// CabSound.exe. The card's FxProperties key is exported to the backups folder first.
/// </summary>
public static class EqApoFix
{
    private const string RenderKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";

    /// <summary>Starts CabSound.exe elevated as the helper; true when it ran and reported success.</summary>
    public static bool RunElevated(string exePath, string deviceId)
    {
        var info = new ProcessStartInfo(exePath) { UseShellExecute = true, Verb = "runas" };
        foreach (var arg in EqApoSetup.FixEnhancementsArgs(deviceId)) info.ArgumentList.Add(arg);
        try
        {
            using var helper = Process.Start(info);
            if (helper is null) return false;
            helper.WaitForExit();
            return helper.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; } // the user said No to the admin prompt
    }

    /// <summary>The helper's work. Returns an error message, or null when done.</summary>
    public static string? TurnOnEnhancements(string deviceId, string backupDir, DateTime now)
    {
        var keyPath = $@"{RenderKey}\{EqApoSetup.EndpointGuid(deviceId)}\FxProperties";
        Directory.CreateDirectory(backupDir);
        var backup = Path.Combine(backupDir, $"FxProperties.{now:yyyyMMdd-HHmmss}.reg");
        if (Run("reg.exe", "export", $@"HKLM\{keyPath}", backup, "/y") != 0) return $"Backup of {keyPath} failed.";

        try
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var fx = hklm.OpenSubKey(keyPath, writable: true);
            if (fx is null) return "The sound card's effect settings were not found.";
            fx.SetValue(EqApoHealth.EnhancementsOffValue, 0, RegistryValueKind.DWord);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return "Windows did not allow the change.";
        }

        // The audio service reads the effect settings when it starts
        Run("net.exe", "stop", "audiosrv", "/y");
        Run("net.exe", "start", "audiosrv");
        return null;
    }

    private static int Run(string exe, params string[] args)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        process.WaitForExit();
        return process.ExitCode;
    }
}
