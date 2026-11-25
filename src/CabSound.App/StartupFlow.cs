using System;
using System.IO;
using System.Linq;
using CabSound.Core;
using CabSound.Platform;
using NAudio.CoreAudioApi;
using Forms = System.Windows.Forms;

namespace CabSound.App;

/// <summary>
/// Start-up: load data → close PinVol (asked) → first-run import → find the USB card (or ask) →
/// warn if its driver changed → check Equalizer APO → take over config.txt (with a preview) → presets (made once) → start running. Returns null when CabSound should exit.
/// </summary>
public static class StartupFlow
{
    public static CabSoundApp? Run()
    {
        try { return RunSteps(); }
        catch (Exception e)
        {
            Log.Write($"Start-up failed: {e}");
            Message($"CabSound could not start:\n\n{e.Message}\n\nDetails in CabSound.log.", Forms.MessageBoxIcon.Error);
            return null;
        }
    }

    private static CabSoundApp? RunSteps()
    {
        Log.Write("CabSound starting");

        // 1. Our data (never silently replaced if it can't be read)
        var firstRun = !File.Exists(Paths.DataFile);
        CabSoundData data;
        try { data = CabSoundDataFile.Load(Paths.DataFile); }
        catch (InvalidDataException e)
        {
            Message($"{e.Message}\n\nCabSound will not start, so your levels aren't lost. Fix or remove the file, then start CabSound again.", Forms.MessageBoxIcon.Error);
            return null;
        }

        // 2. PinVol can't run alongside (every press would change levels twice)
        string? pinVolExe = null;
        if (PinVolProcess.Running() is { } pinVol)
        {
            var answer = Forms.MessageBox.Show("PinVol is running. Close PinVol and let CabSound take over?",
                "CabSound", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question);
            if (answer != Forms.DialogResult.Yes) { Log.Write("PinVol kept; CabSound exits"); return null; }
            pinVolExe = PinVolProcess.ExePath(pinVol);
            PinVolProcess.Close(pinVol);
            Log.Write($"PinVol closed ({pinVolExe})");
        }

        // 3. First run: import PinVol's tables and pop-up position if present, or start fresh
        if (firstRun)
        {
            var dir = FindPinVolDir(pinVolExe);
            if (dir is not null)
            {
                data = Import(dir, data);
                Log.Write($"Imported {data.Tables.Count} tables from {dir}");
            }
            else
            {
                Log.Write("First run: starting fresh without PinVol import");
            }
            CabSoundDataFile.Save(Paths.DataFile, data);
        }

        // 3b. Pop-up/menu placement: saved, else migrated from PinVol's box, else defaults (saved once)
        var hadOverlays = data.Overlays is not null;
        data = data with { Overlays = OverlayLayout.Resolve(data, Monitors.All()) };
        if (!hadOverlays)
        {
            CabSoundDataFile.Save(Paths.DataFile, data);
            Log.Write("Overlay placement saved (migrated from PinVol's pop-up box)");
        }

        // 4. The USB card (by ID, or by name if Windows renumbered it; the owner picks it if both fail,
        //    e.g. after a new driver renamed it)
        var savedId = data.DeviceId;
        using var initialDevice = AudioDevice.Find(data.DeviceId, data.DeviceName);
        MMDevice? pickedDevice = null;
        var device = initialDevice;
        if (device is null && SoundCardDialog.Pick(data.DeviceName, AudioDevice.Outputs()) is { } picked)
        {
            Log.Write($"Sound card '{data.DeviceName}' not found; owner picked '{picked.Name}'");
            data = data with { DeviceId = picked.Id, DeviceName = SoundCardChoice.NameHint(picked.Name) };
            pickedDevice = AudioDevice.Find(data.DeviceId, data.DeviceName);
            device = pickedDevice;
        }
        using var _ = pickedDevice;
        Log.Write(device is null ? $"Sound card '{data.DeviceName}' not found" : $"Sound card: {device.FriendlyName}");

        // 4b. A new driver or another output can move the Side and Rear jacks: tell the owner to check
        if (device is not null)
        {
            var driver = AudioDevice.Driver(device);
            Log.Write($"Sound card driver: {driver ?? "unknown"}");
            var advice = ChannelCheckAdvice.For(data.DeviceDriver, driver, cardChanged: savedId is not null && savedId != device.ID);
            var seen = data with { DeviceId = device.ID, DeviceDriver = driver ?? data.DeviceDriver };
            if (seen != data)
            {
                data = seen;
                CabSoundDataFile.Save(Paths.DataFile, data);
            }
            if (advice is not null)
            {
                Log.Write("Channel check advised (driver or sound card changed)");
                Message(advice, Forms.MessageBoxIcon.Warning);
            }
        }

        // 5. Equalizer APO installed / on the card? (can't check the card if it wasn't found)
        EqApoStatus? apo = device is null
            ? (File.Exists(Paths.EqApoConfig) ? null : EqApoStatus.NotInstalled)
            : EqApoCheck.Status(device.ID);
        Log.Write($"Equalizer APO: {apo?.ToString() ?? "not checked (no sound card)"}");
        if (device is not null && apo is { } status && status != EqApoStatus.Active)
        {
            apo = EqApoSetupDialog.Run(device.ID, device.FriendlyName, status);
            Log.Write($"Equalizer APO after setup: {apo}");
        }

        // 6. config.txt → one Include line for CabSound.txt, after the owner has seen what changes
        //    (also when something else rewrote config.txt since the last run)
        if (apo != EqApoStatus.NotInstalled && SwitchOver.NeedsTakeOver(Paths.EqApoConfig))
        {
            try
            {
                var text = File.ReadAllText(Paths.EqApoConfig);
                var found = ConfigReader.Read(text);
                if (!TakeOverDialog.Confirm(text, found, Paths.Backups))
                {
                    Log.Write("Take-over cancelled; exit");
                    RestartPinVol(pinVolExe);
                    return null;
                }
                data = SwitchOver.TakeOver(Paths.EqApoConfig, Paths.Backups, DateTime.Now, data, found,
                    d => CabSoundDataFile.Save(Paths.DataFile, d));
                Log.Write($"config.txt now includes CabSound.txt (backup: {data.SwitchOverBackup})");
                foreach (var line in found.NotUnderstood) Log.Write($"Take-over lost line: {line}");
            }
            catch
            {
                RestartPinVol(pinVolExe); // don't leave the cabinet without a volume program
                throw;
            }
        }

        // 7. One-time migration: if CabSound.json still has legacy presets, bake each table's effective
        //    levels and low cuts into the table row (backing up CabSound.json first).
        var (migrated, changed) = LegacyPresetMigration.Apply(data);
        if (changed)
        {
            if (File.Exists(Paths.DataFile))
                Log.Write($"Migrated legacy presets into table levels (backup: {SafeFileWriter.Backup(Paths.DataFile, Paths.Backups, DateTime.Now)})");
            CabSoundDataFile.Save(Paths.DataFile, migrated);
            data = migrated;
        }

        return new CabSoundApp(data, apo, deviceFound: device is not null);
    }

    /// <summary>Start-up closed PinVol but CabSound isn't taking over after all: leave things as they were.</summary>
    private static void RestartPinVol(string? pinVolExe)
    {
        try { if (pinVolExe is not null) PinVolProcess.Start(pinVolExe); }
        catch (Exception e) { Log.Write($"Restarting PinVol failed: {e.Message}"); }
    }

    private static CabSoundData Import(string pinVolDir, CabSoundData data)
    {
        var tables = PinVolTablesFile.Parse(File.ReadAllText(Path.Combine(pinVolDir, "PinVolTables.ini")));
        var imported = PinVolImport.FromPinVol(tables);
        var settings = Path.Combine(pinVolDir, "PinVolSettings.ini");
        var display = File.Exists(settings) ? PinVolSettingsImport.Display(File.ReadAllText(settings)) : null;
        return data with { Calibration = imported.Calibration, Tables = imported.Tables, Display = display };
    }

    private static string? FindPinVolDir(string? pinVolExe)
    {
        foreach (var dir in new[] { pinVolExe is null ? null : Path.GetDirectoryName(pinVolExe) }.Concat(Paths.PinVolDesktopDirs()))
            if (dir is not null && File.Exists(Path.Combine(dir, "PinVolTables.ini"))) return dir;

        var ask = Forms.MessageBox.Show(
            "Do you want to import existing table levels from a PinVol folder?\n\nClick No to start fresh.",
            "CabSound — First Run", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question);
        if (ask != Forms.DialogResult.Yes) return null;

        using var pick = new Forms.FolderBrowserDialog { Description = "Pick the PinVol folder (with PinVolTables.ini) to import your table levels." };
        while (pick.ShowDialog() == Forms.DialogResult.OK)
        {
            if (File.Exists(Path.Combine(pick.SelectedPath, "PinVolTables.ini"))) return pick.SelectedPath;
            Message("That folder has no PinVolTables.ini.", Forms.MessageBoxIcon.Warning);
        }
        return null;
    }

    private static void Message(string text, Forms.MessageBoxIcon icon) =>
        Forms.MessageBox.Show(text, "CabSound", Forms.MessageBoxButtons.OK, icon);
}
