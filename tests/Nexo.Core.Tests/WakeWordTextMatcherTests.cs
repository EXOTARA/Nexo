using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class WakeWordTextMatcherTests
{
    [Theory]
    [InlineData("Kohana")]
    [InlineData("koana")]
    [InlineData("  KÓHANA  ")]
    [InlineData("oye kohana")]
    [InlineData("hey kohana")]
    [InlineData("ey kohana")]
    [InlineData("e kohana")]
    [InlineData("hey cohana")]
    [InlineData("ey ko ana")]
    [InlineData("Nexo")]
    [InlineData("oye nexo")]
    public void KohanaMode_AcceptsNewAndLegacyPhrases(string text)
    {
        Assert.True(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.Kohana));
    }

    [Theory]
    [InlineData("cohana baja spotify")]
    [InlineData("kohana baja spotify")]
    [InlineData("oye")]
    [InlineData("")]
    public void KohanaMode_RejectsLongerOrIncompletePhrases(string text)
    {
        Assert.False(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.Kohana));
    }

    [Theory]
    [InlineData("oye kohana")]
    [InlineData("¡Oye, Kohana!")]
    [InlineData("oye koana")]
    [InlineData("oye nexo")]
    public void OyeKohanaMode_AcceptsRecommendedAndLegacyPhrase(string text)
    {
        Assert.True(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.OyeKohana));
        Assert.False(WakeWordTextMatcher.IsMatch("kohana", WakeWordPhrase.OyeKohana));
    }

    [Theory]
    [InlineData("hey kohana")]
    [InlineData("ey kohana")]
    [InlineData("ei kohana")]
    [InlineData("ahí kohana")]
    [InlineData("hey nexo")]
    public void HeyKohanaMode_AcceptsCommonRecognitionVariants(string text)
    {
        Assert.True(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.HeyKohana));
        Assert.False(WakeWordTextMatcher.IsMatch("kohana", WakeWordPhrase.HeyKohana));
    }

    [Theory]
    [InlineData("kohana")]
    [InlineData("koana")]
    [InlineData("hola kohana")]
    public void HighSensitivity_AcceptsShortOrApproximateEnding(string text)
    {
        Assert.True(WakeWordTextMatcher.IsMatch(
            text,
            WakeWordPhrase.HeyKohana,
            WakeWordSensitivity.High));
    }

    [Fact]
    public void StrictSensitivity_RejectsRecognitionVariants()
    {
        Assert.False(WakeWordTextMatcher.IsMatch(
            "e koana",
            WakeWordPhrase.HeyKohana,
            WakeWordSensitivity.Strict));
        Assert.True(WakeWordTextMatcher.IsMatch(
            "ey kohana",
            WakeWordPhrase.HeyKohana,
            WakeWordSensitivity.Strict));
    }

    [Theory]
    [InlineData("Nexo")]
    [InlineData("oye nexo")]
    [InlineData("hey nexo")]
    public void LegacyModes_RemainReadableDuringMigration(string text)
    {
        var phrase = text.StartsWith("oye", StringComparison.OrdinalIgnoreCase)
            ? WakeWordPhrase.OyeNexo
            : text.StartsWith("hey", StringComparison.OrdinalIgnoreCase)
                ? WakeWordPhrase.HeyNexo
                : WakeWordPhrase.Nexo;

        Assert.True(WakeWordTextMatcher.IsMatch(text, phrase));
    }
}
