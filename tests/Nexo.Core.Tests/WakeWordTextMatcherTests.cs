using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class WakeWordTextMatcherTests
{
    [Theory]
    [InlineData("Nexo")]
    [InlineData("nexo.")]
    [InlineData("  NÉXO  ")]
    [InlineData("oye nexo")]
    [InlineData("hey nexo")]
    [InlineData("ey nexo")]
    [InlineData("ahí nexo")]
    [InlineData("oi nexo")]
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
    [Theory]
    [InlineData("hey nexo")]
    [InlineData("ey nexo")]
    [InlineData("ei nexo")]
    [InlineData("ahí nexo")]
    [InlineData("hey neso")]
    [InlineData("¡Hey, Nexo!")]
    public void HeyNexoMode_AcceptsCommonSpanishRecognitionVariants(string text)
    {
        Assert.True(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.HeyNexo));
        Assert.False(WakeWordTextMatcher.IsMatch("nexo", WakeWordPhrase.HeyNexo));
    }

}
