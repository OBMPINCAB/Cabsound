using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public sealed class SwitchOverTests : IDisposable
{
    private const string RealConfig =
        "Channel: all\r\nPreamp: -5 dB\r\n" +
        "Channel: SL SR RL RR\r\nFilter: ON HP Fc 90 Hz\r\nFilter: ON HP Fc 90 Hz\r\n" +
        "Channel: L R\r\nFilter: ON HP Fc 90 Hz\r\nFilter: ON HP Fc 90 Hz\r\n" +
        "Channel: all\r\nInclude: PinVolSSF.txt\r\nCopy: SL=RL SR=RR RL=SL RR=SR\r\n";

    private readonly string _dir = Directory.CreateTempSubdirectory("cabsound-test-").FullName;
    private string Config => Path.Combine(_dir, "config.txt");
    private string CabSoundTxt => Path.Combine(_dir, "CabSound.txt");
    private string Backups => Path.Combine(_dir, "backups");
    private static readonly DateTime Now = new(2026, 11, 10, 6, 1, 16);

    public SwitchOverTests() => File.WriteAllText(Config, RealConfig);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private CabSoundData TakeOver(CabSoundData? data = null) =>
        SwitchOver.TakeOver(Config, Backups, Now, data ?? CabSoundData.Defaults(), ConfigReader.Read(RealConfig), _ => { });

    [Fact]
    public void NeedsTakeOver_TrueForPinVolConfig_FalseForManaged_FalseWhenMissing()
    {
        Assert.True(SwitchOver.NeedsTakeOver(Config));

        File.WriteAllText(Config, ManagedConfig.Text);
        Assert.False(SwitchOver.NeedsTakeOver(Config));

        File.Delete(Config);
        Assert.False(SwitchOver.NeedsTakeOver(Config));
    }

    [Fact]
    public void TakeOver_BacksUp_WritesCabSoundTxt_ThenManagedConfig()
    {
        var data = TakeOver();

        Assert.Equal(ManagedConfig.Text, File.ReadAllText(Config));
        Assert.Equal(RealConfig, File.ReadAllText(data.SwitchOverBackup!));
        Assert.StartsWith(Backups, data.SwitchOverBackup);
        Assert.Equal(new SoundSettings(LowCutMode.Original, Dip250: false, JacksSwapped: true), data.Sound);
        Assert.False(SwitchOver.NeedsTakeOver(Config));
    }

    [Fact]
    public void TakeOver_CabSoundTxt_HasTheSystemLevelsAndTheFoundSound()
    {
        var before = CabSoundData.Defaults() with
        {
            Calibration = new GroupLevels(-10, 10, 10, 0),
            SsfBoostLimitDb = 0,
            Tables = [new TableLevel("System", 70, 66, 0, 0, -4)],
        };

        var data = TakeOver(before);

        Assert.Equal(
            EqApoFile.Build(before.Tables[0], before.Calibration, data.Sound!, false, 0),
            File.ReadAllText(CabSoundTxt));
        Assert.EndsWith("Copy: SL=RL SR=RR RL=SL RR=SR\r\n", File.ReadAllText(CabSoundTxt));
    }

    [Fact]
    public void TakeOver_KeepsRattleWarned()
    {
        var data = TakeOver(CabSoundData.Defaults() with { Sound = new SoundSettings(RattleWarned: true) });

        Assert.True(data.Sound!.RattleWarned);
    }

    [Fact]
    public void TakeOver_WhenCabSoundTxtCannotBeWritten_LeavesConfigUntouched()
    {
        // The Include must never point at a missing file: CabSound.txt is written before config.txt
        Directory.CreateDirectory(CabSoundTxt); // a folder with that name makes the write fail

        Assert.ThrowsAny<Exception>(() => TakeOver());

        Assert.Equal(RealConfig, File.ReadAllText(Config));
    }

    [Fact]
    public void TakeOver_SavesTheSettingsBeforeAnyFileIsWritten()
    {
        // If CabSound dies mid take-over, the jack swap must already be saved: a managed config.txt
        // with unsaved settings would silently fall back to "not swapped"
        CabSoundData? saved = null;
        var filesAtSave = "";

        SwitchOver.TakeOver(Config, Backups, Now, CabSoundData.Defaults(), ConfigReader.Read(RealConfig), d =>
        {
            saved = d;
            filesAtSave = $"{File.ReadAllText(Config) == RealConfig} {File.Exists(CabSoundTxt)}";
        });

        Assert.True(saved!.Sound!.JacksSwapped);
        Assert.NotNull(saved.SwitchOverBackup);
        Assert.Equal("True False", filesAtSave);
    }

    [Fact]
    public void TakeOver_OfAChangedManagedConfig_KeepsTheSavedSound()
    {
        var sound = new SoundSettings(LowCutMode.Gentle, Dip250: true, JacksSwapped: true);
        var changed = ManagedConfig.Text + "Preamp: -3 dB\r\n";
        File.WriteAllText(Config, changed);

        var data = SwitchOver.TakeOver(Config, Backups, Now, CabSoundData.Defaults() with { Sound = sound }, ConfigReader.Read(changed), _ => { });

        Assert.Equal(sound, data.Sound);
        Assert.Contains("Channel: L R\r\nFilter: ON HP Fc 40 Hz\r\n", File.ReadAllText(CabSoundTxt)); // the saved low cuts are written
        Assert.Contains("Channel: RL RR\r\nFilter: ON HP Fc 60 Hz\r\n", File.ReadAllText(CabSoundTxt));
        Assert.Contains("Copy: SL=RL SR=RR RL=SL RR=SR", File.ReadAllText(CabSoundTxt));
        Assert.Equal(ManagedConfig.Text, File.ReadAllText(Config));
    }
}
