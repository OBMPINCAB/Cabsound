using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class SoundCardChoiceTests
{
    private const string Hint = "USB Sound Device";

    [Fact]
    public void KnownId_Wins()
    {
        var id = SoundCardChoice.Pick([("a", "Speakers (3- USB Sound Device)"), ("b", "Realtek")], "b", Hint);

        Assert.Equal("b", id);
    }

    [Fact]
    public void IdGone_FallsBackToName()
    {
        Assert.Equal("a", SoundCardChoice.Pick([("a", "Speakers (3- USB Sound Device)")], "old", Hint));
    }

    [Fact]
    public void SeveralNameMatches_PrefersSpeakersOutput()
    {
        // Many USB 7.1 cards also list a digital output with the same name
        var id = SoundCardChoice.Pick(
            [("spdif", "Digital Output (3- USB Sound Device)"), ("spk", "Speakers (3- USB Sound Device)")], null, Hint);

        Assert.Equal("spk", id);
    }

    [Fact]
    public void NoMatch_IsNull()
    {
        Assert.Null(SoundCardChoice.Pick([("x", "Realtek HD Audio")], null, Hint));
    }

    [Fact]
    public void NewDriverRenamedTheCard_IsNotFound()
    {
        // 2026-11-11: the C-Media driver replaced Microsoft's; new ID and a new name
        Assert.Null(SoundCardChoice.Pick([("new", "Speakers (USB Multi-Channel Audio Device)")], "old", Hint));
    }

    [Theory]
    [InlineData("Speakers (USB Multi-Channel Audio Device)", "USB Multi-Channel Audio Device")]
    [InlineData("Speakers (3- USB Sound Device)", "USB Sound Device")]          // Windows' port number is dropped
    [InlineData("Speakers (12- USB Sound Device        )", "USB Sound Device")]
    [InlineData("Speakers (Realtek(R) Audio)", "Realtek(R) Audio")]
    [InlineData("Some Output", "Some Output")]
    public void NameHint_IsTheCardNameWithoutOutputAndPortNumber(string friendlyName, string hint)
    {
        Assert.Equal(hint, SoundCardChoice.NameHint(friendlyName));
    }

    [Fact]
    public void NameHint_FindsTheSameCardAfterWindowsRenumbersIt()
    {
        var hint = SoundCardChoice.NameHint("Speakers (3- USB Sound Device)");

        Assert.Equal("a", SoundCardChoice.Pick([("a", "Speakers (4- USB Sound Device)")], "old", hint));
    }
}
