using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class ConfigReaderTests
{
    private static string Data(string name) => File.ReadAllText(Path.Combine("TestData", name));

    [Fact]
    public void Read_LiveConfigOf20261110_Original_Dip_Swapped_NothingNotUnderstood()
    {
        var f = ConfigReader.Read(Data("config-20261110-2035.txt"));

        Assert.Equal(new SoundSettings(LowCutMode.Original, Dip250: true, JacksSwapped: true), f.Sound);
        Assert.Empty(f.NotUnderstood);
        Assert.Single(f.Notes); // the swap sat above the level file
        Assert.Contains(f.Dropped, l => l.Contains("Preamp: -2 dB"));
        Assert.Contains(f.Dropped, l => l.Contains("Include: PinVolSSF.txt"));
        Assert.Contains(f.Dropped, l => l.Contains("LP Fc 120 Hz")); // dead SUB block
        Assert.Equal(3, f.Carried.Count); // low cut, dip, jack swap
    }

    [Fact]
    public void Read_OriginalPinVolConfig_Original_Dip_NotSwapped()
    {
        var f = ConfigReader.Read(Data("config-pinvol-original.txt"));

        Assert.Equal(new SoundSettings(LowCutMode.Original, Dip250: true, JacksSwapped: false), f.Sound);
        Assert.Empty(f.NotUnderstood);
        Assert.Contains(f.Dropped, l => l.Contains("Preamp: -5 dB"));
    }

    [Fact]
    public void Read_GentleCuts_IsGentle()
    {
        var f = ConfigReader.Read("Channel: L R\r\nFilter: ON HP Fc 40 Hz\r\nChannel: SL SR RL RR\r\nFilter: ON HP Fc 60 Hz\r\n");

        Assert.Equal(LowCutMode.Gentle, f.Sound.LowCut);
        Assert.False(f.Sound.Dip250);
        Assert.Empty(f.NotUnderstood);
        Assert.Empty(f.Notes);
    }

    [Fact]
    public void Read_OddLowCuts_StartsOnOriginal_ListsThem_AndSaysSo()
    {
        var f = ConfigReader.Read("Channel: L R\r\nFilter: ON HP Fc 90 Hz\r\nFilter: ON HP Fc 90 Hz\r\nChannel: SL SR RL RR\r\nFilter: ON HP Fc 70 Hz\r\n");

        Assert.Equal(LowCutMode.Original, f.Sound.LowCut);
        Assert.Contains("Filter: ON HP Fc 70 Hz", f.NotUnderstood);
        Assert.Single(f.Notes);
    }

    [Fact]
    public void Read_NoLowCutsAtAll_SaysOriginalWillAddThem()
    {
        var f = ConfigReader.Read("Include: PinVolSSF.txt");

        Assert.Equal(LowCutMode.Original, f.Sound.LowCut);
        Assert.False(f.Sound.Dip250);
        Assert.Empty(f.NotUnderstood);
        Assert.Contains("90 Hz", Assert.Single(f.Notes));
    }

    [Fact]
    public void Read_LfOnly_LowerCase_ExtraSpaces_ReadsTheSame()
    {
        var f = ConfigReader.Read("channel:  l r\nfilter:  on hp fc 90 hz\nfilter: on hp fc 90 hz\nfilter: on pk fc 250 hz gain -2 db q 1.0\n" +
                                  "  channel: sl sr rl rr\nfilter: on hp fc 90 hz\nfilter: on hp fc 90 hz\nfilter: ON PK Fc 250 Hz Gain -2 dB Q 1\ncopy: sl=rl sr=rr rl=sl rr=sr\n");

        Assert.Equal(new SoundSettings(LowCutMode.Original, Dip250: true, JacksSwapped: true), f.Sound);
        Assert.Empty(f.NotUnderstood);
    }

    [Fact]
    public void Read_UnknownLines_AreNotUnderstood()
    {
        var f = ConfigReader.Read("Include: ThumpSSF.txt\r\nDelay: 5 ms\r\nCopy: SUB=0.5*L+0.5*R\r\nDevice: Speakers\r\nChannel: L R\r\nFilter: ON LP Fc 8000 Hz\r\n");

        Assert.Equal(["Include: ThumpSSF.txt", "Delay: 5 ms", "Copy: SUB=0.5*L+0.5*R", "Device: Speakers", "Filter: ON LP Fc 8000 Hz"], f.NotUnderstood);
    }

    [Fact]
    public void Read_DipOnOneGroupOnly_IsNotUnderstood()
    {
        var f = ConfigReader.Read("Channel: L R\r\nFilter: ON PK Fc 250 Hz Gain -2 dB Q 1.0\r\n");

        Assert.False(f.Sound.Dip250);
        Assert.Single(f.NotUnderstood);
    }

    [Fact]
    public void Read_CommentsAndBlankLines_AreIgnored()
    {
        var f = ConfigReader.Read("# Include: Other.txt\r\n\r\n   \r\n");

        Assert.Empty(f.NotUnderstood);
        Assert.Empty(f.Dropped);
    }

    [Fact]
    public void IsOurs_OnlyForTheManagedText()
    {
        Assert.True(ManagedConfig.IsOurs(ManagedConfig.Text));
        Assert.True(ManagedConfig.IsOurs(ManagedConfig.Text.Replace("\r\n", "\n") + "\n\n"));
        Assert.False(ManagedConfig.IsOurs(ManagedConfig.Text + "Preamp: 1 dB\r\n"));
        Assert.False(ManagedConfig.IsOurs(Data("config-20261110-2035.txt")));
        Assert.Equal("# Managed by CabSound -- do not edit (change sound settings in CabSound)\r\nInclude: CabSound.txt\r\n", ManagedConfig.Text);
    }

    [Fact]
    public void Read_CabSoundsOwnConfigWithExtraLines_KeepsTheSavedSound_AndListsTheExtras()
    {
        // Something else (e.g. the Equalizer APO editor) changed the managed config.txt: the saved
        // jack swap, dip and low cut must not be reset just because config.txt has no filters
        var f = ConfigReader.Read(ManagedConfig.Text + "Preamp: -3 dB\r\nChannel: L R\r\nFilter: ON HP Fc 90 Hz\r\n");

        Assert.True(f.KeepSavedSound);
        Assert.Equal(["Filter: ON HP Fc 90 Hz"], f.NotUnderstood);
        Assert.Contains(f.Dropped, l => l == "Preamp: -3 dB");
        Assert.Single(f.Carried);
        Assert.Empty(f.Notes);
    }

    [Fact]
    public void Read_PinVolConfig_DoesNotKeepSavedSound()
    {
        Assert.False(ConfigReader.Read(Data("config-20261110-2035.txt")).KeepSavedSound);
    }

    [Fact]
    public void Read_SwapAboveTheLevelFile_NotesThatFrontRearBalanceMayDiffer()
    {
        // Levels applied after the swap landed on the other exciter pair; CabSound applies them before it
        var above = ConfigReader.Read("Copy: SL=RL SR=RR RL=SL RR=SR\r\nInclude: PinVolSSF.txt\r\n");
        var below = ConfigReader.Read("Include: PinVolSSF.txt\r\nCopy: SL=RL SR=RR RL=SL RR=SR\r\n");

        Assert.Contains(above.Notes, n => n.Contains("front and rear"));
        Assert.DoesNotContain(below.Notes, n => n.Contains("front and rear"));
        Assert.DoesNotContain(ConfigReader.Read(Data("config-pinvol-original.txt")).Notes, n => n.Contains("front and rear"));
        Assert.Contains(ConfigReader.Read(Data("config-20261110-2035.txt")).Notes, n => n.Contains("front and rear"));
    }
}
