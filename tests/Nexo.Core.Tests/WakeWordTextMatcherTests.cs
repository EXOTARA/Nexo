using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class WakeWordTextMatcherTests
{
    [Theory]
    [InlineData("Nexo")]
    [InlineData("nexo.")]
    [InlineData("  NÉXO  ")]
    [InlineData("oye nexo")]
    public void NexoMode_AcceptsConfiguredPhrases(string text)
    {
        Assert.True(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.Nexo));
    }

    [Theory]
    [InlineData("anexo")]
    [InlineData("sexo")]
    [InlineData("nexo baja spotify")]
    [InlineData("oye")]
    [InlineData("")]
    public void NexoMode_RejectsNearOrLongerPhrases(string text)
    {
        Assert.False(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.Nexo));
    }

    [Theory]
    [InlineData("oye nexo")]
    [InlineData("¡Oye, Nexo!")]
    public void OyeNexoMode_RequiresFullPhrase(string text)
    {
        Assert.True(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.OyeNexo));
        Assert.False(WakeWordTextMatcher.IsMatch("nexo", WakeWordPhrase.OyeNexo));
    }
}
