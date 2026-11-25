using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public sealed class SafeFileWriterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cabsound-test-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Write_CreatesNewFile()
    {
        var path = Path.Combine(_dir, "a.txt");

        SafeFileWriter.Write(path, "hello");

        Assert.Equal("hello", File.ReadAllText(path));
    }

    [Fact]
    public void Write_ReplacesExistingFile_AndLeavesNoTempFile()
    {
        var path = Path.Combine(_dir, "a.txt");
        File.WriteAllText(path, "old");

        SafeFileWriter.Write(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }

    [Fact]
    public void Write_RetriesWhileAnotherProgramBrieflyHoldsTheFile()
    {
        // Seen on the cabinet: Equalizer APO reads the include file at the moment CabSound replaces it
        var path = Path.Combine(_dir, "a.txt");
        File.WriteAllText(path, "old");
        var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var release = Task.Delay(150).ContinueWith(_ => reader.Dispose());

        SafeFileWriter.Write(path, "new");

        release.Wait();
        Assert.Equal("new", File.ReadAllText(path));
    }

    [Fact]
    public void Write_HasNoByteOrderMark_LikeEqualizerApoFiles()
    {
        var path = Path.Combine(_dir, "a.txt");

        SafeFileWriter.Write(path, "# x");

        Assert.Equal((byte)'#', File.ReadAllBytes(path)[0]);
    }

    [Fact]
    public void Backup_CopiesWithTimestamp_IntoBackupFolder()
    {
        var path = Path.Combine(_dir, "config.txt");
        File.WriteAllText(path, "original");
        var backups = Path.Combine(_dir, "backups");

        var copy = SafeFileWriter.Backup(path, backups, new DateTime(2026, 11, 10, 6, 1, 16));

        Assert.Equal(Path.Combine(backups, "config.txt.20261110-060116.bak"), copy);
        Assert.Equal("original", File.ReadAllText(copy));
    }

    [Fact]
    public void Backup_SameSecondTwice_KeepsBoth()
    {
        var path = Path.Combine(_dir, "config.txt");
        var backups = Path.Combine(_dir, "backups");
        var now = new DateTime(2026, 11, 10, 6, 1, 16);
        File.WriteAllText(path, "one");
        var first = SafeFileWriter.Backup(path, backups, now);
        File.WriteAllText(path, "two");

        var second = SafeFileWriter.Backup(path, backups, now);

        Assert.NotEqual(first, second);
        Assert.Equal("one", File.ReadAllText(first));
        Assert.Equal("two", File.ReadAllText(second));
    }
}
