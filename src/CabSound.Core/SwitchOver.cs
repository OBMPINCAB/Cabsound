namespace CabSound.Core;

/// <summary>
/// Take-over of Equalizer APO's config.txt: from PinVol's or a hand-written file to CabSound's one
/// Include line. The old file is backed up first and kept (going back = copy it back by hand).
/// </summary>
public static class SwitchOver
{
    /// <summary>True when config.txt exists and isn't CabSound's managed file.</summary>
    public static bool NeedsTakeOver(string configPath) =>
        File.Exists(configPath) && !ManagedConfig.IsOurs(File.ReadAllText(configPath));

    /// <summary>
    /// Backs up config.txt, saves the settings (the sound settings found in the old file, or the saved
    /// ones if the old file was already CabSound's, plus the backup's path), writes CabSound.txt next to
    /// config.txt (the "System" levels), then the managed config.txt. In that order: the Include never
    /// points at a missing file, and a managed config.txt never exists without its saved settings.
    /// </summary>
    public static CabSoundData TakeOver(string configPath, string backupDir, DateTime now, CabSoundData data,
        FoundConfig found, Action<CabSoundData> save)
    {
        var sound = found.KeepSavedSound
            ? data.SoundOrDefault
            : found.Sound with { RattleWarned = data.SoundOrDefault.RattleWarned };
        var system = data.Tables.FirstOrDefault(t => t.Name == PinVolImport.SystemRow)
            ?? new TableLevel(PinVolImport.SystemRow, 100, 100, 0, 0, 0);
        data = data with { Sound = sound, SwitchOverBackup = SafeFileWriter.Backup(configPath, backupDir, now) };
        save(data);

        SafeFileWriter.Write(Path.Combine(Path.GetDirectoryName(configPath)!, EqApoFile.FileName),
            EqApoFile.Build(WithCabinetCuts(system, sound), data.Calibration, sound, data.SubConnected, data.SsfBoostLimitDb));
        SafeFileWriter.Write(configPath, ManagedConfig.Text);
        return data;
    }

    private static TableLevel WithCabinetCuts(TableLevel system, SoundSettings sound) => system with
    {
        BackboxLowCut = LowCutChoices.Effective(sound, LowCutGroup.Backbox, system.BackboxLowCut),
        ExciterLowCut = LowCutChoices.Effective(sound, LowCutGroup.Exciters, system.ExciterLowCut),
        RearExciterLowCut = LowCutChoices.Effective(sound, LowCutGroup.RearExciters, system.RearExciterLowCut ?? (sound.RearExciterCut is null ? system.ExciterLowCut : null)),
    };
}
