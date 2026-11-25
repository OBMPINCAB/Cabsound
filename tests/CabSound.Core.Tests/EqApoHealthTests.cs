using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class EqApoHealthTests
{
    const string Apo = "{EC1CC9CE-FAED-4822-828A-82A81A6F018F}";
    const string ApoValue = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},2";
    const string EnhancementsOffValue = "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5";

    static Dictionary<string, object?> Fx(params (string Name, object? Value)[] values) =>
        values.ToDictionary(v => v.Name, v => v.Value);

    [Fact]
    public void NotInstalled_WhenEqualizerApoIsMissing()
    {
        Assert.Equal(EqApoStatus.NotInstalled, EqApoHealth.Status(installed: false, Fx((ApoValue, Apo))));
    }

    [Fact]
    public void NotOnDevice_WhenTheCardHasNoEffects()
    {
        Assert.Equal(EqApoStatus.NotOnDevice, EqApoHealth.Status(installed: true, null));
    }

    [Fact]
    public void NotOnDevice_WhenAnotherEffectIsRegistered()
    {
        var fx = Fx((ApoValue, "{C9453E73-8C5C-4463-9984-AF8BAB2F5447}"));
        Assert.Equal(EqApoStatus.NotOnDevice, EqApoHealth.Status(true, fx));
    }

    [Fact]
    public void Active_WhenRegisteredOnTheCard()
    {
        Assert.Equal(EqApoStatus.Active, EqApoHealth.Status(true, Fx((ApoValue, Apo))));
    }

    [Fact]
    public void Active_WhenRegisteredInAListOrInLowerCase()
    {
        Assert.Equal(EqApoStatus.Active, EqApoHealth.Status(true, Fx((ApoValue, new[] { "{other}", Apo }))));
        Assert.Equal(EqApoStatus.Active, EqApoHealth.Status(true, Fx((ApoValue, Apo.ToLowerInvariant()))));
    }

    // Cabinet 2026-11-12: registered, but Windows' "audio enhancements" were off, so no level applied
    [Fact]
    public void EnhancementsOff_WhenWindowsBypassesAllEffects()
    {
        var fx = Fx((ApoValue, Apo), (EnhancementsOffValue, 1));
        Assert.Equal(EqApoStatus.EnhancementsOff, EqApoHealth.Status(true, fx));
    }

    [Fact]
    public void EnhancementsOff_NameIsNotCaseSensitive()
    {
        var fx = Fx((ApoValue, Apo), (EnhancementsOffValue.ToUpperInvariant(), 1));
        Assert.Equal(EqApoStatus.EnhancementsOff, EqApoHealth.Status(true, fx));
    }

    [Fact]
    public void Active_WhenEnhancementsAreOn()
    {
        var fx = Fx((ApoValue, Apo), (EnhancementsOffValue, 0));
        Assert.Equal(EqApoStatus.Active, EqApoHealth.Status(true, fx));
    }

    [Fact]
    public void NotOnDevice_WinsOverEnhancementsOff()
    {
        Assert.Equal(EqApoStatus.NotOnDevice, EqApoHealth.Status(true, Fx((EnhancementsOffValue, 1))));
    }

    [Fact]
    public void Active_HasNoWarning()
    {
        Assert.Null(EqApoHealth.Warning(EqApoStatus.Active));
    }

    [Theory]
    [InlineData(EqApoStatus.NotInstalled, "isn't installed")]
    [InlineData(EqApoStatus.NotOnDevice, "Fix Equalizer APO")]
    [InlineData(EqApoStatus.EnhancementsOff, "Audio enhancements")]
    public void EveryProblem_HasAWarningThatSaysLevelsWontApply(EqApoStatus status, string part)
    {
        var text = EqApoHealth.Warning(status);
        Assert.Contains(part, text);
        Assert.Contains("won't apply", text);
    }
}
