using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class WakeWordTextMatcherTests
{
    [Theory]
    [InlineData("Kohana", WakeWordMatchKind.Exact)]
    [InlineData("cojana", WakeWordMatchKind.Phonetic)]
    [InlineData("kojana", WakeWordMatchKind.Phonetic)]
    [InlineData("co jana", WakeWordMatchKind.Phonetic)]
    [InlineData("coja ana", WakeWordMatchKind.Phonetic)]
    [InlineData("juana", WakeWordMatchKind.Phonetic)]
    [InlineData("oye cojana", WakeWordMatchKind.Phonetic)]
    [InlineData("ey kojana", WakeWordMatchKind.Phonetic)]
    public void KohanaMode_AcceptsSpanishPronunciation(
        string text,
        WakeWordMatchKind expectedKind)
    {
        var result = WakeWordTextMatcher.Evaluate(text, WakeWordPhrase.Kohana);

        Assert.True(result.IsMatch);
        Assert.Equal(expectedKind, result.Kind);
    }

    [Theory]
    [InlineData("Nexo")]
    [InlineData("oye nexo")]
    [InlineData("hey nexo")]
    public void KohanaModes_DoNotAcceptLegacyBrand(string text)
    {
        Assert.False(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.Kohana));
        Assert.False(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.OyeKohana));
        Assert.False(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.HeyKohana));
    }

    [Theory]
    [InlineData("oye cojana")]
    [InlineData("oi kojana")]
    [InlineData("oye kohana")]
    public void OyeKohanaMode_AcceptsExpectedFamily(string text)
    {
        Assert.True(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.OyeKohana));
        Assert.False(WakeWordTextMatcher.IsMatch("ey cojana", WakeWordPhrase.OyeKohana));
    }

    [Theory]
    [InlineData("ey cojana")]
    [InlineData("hey kojana")]
    [InlineData("e cojana")]
    [InlineData("jei kohana")]
    public void HeyKohanaMode_AcceptsSpanishVariants(string text)
    {
        Assert.True(WakeWordTextMatcher.IsMatch(text, WakeWordPhrase.HeyKohana));
        Assert.False(WakeWordTextMatcher.IsMatch("oye cojana", WakeWordPhrase.HeyKohana));
    }

    [Fact]
    public void HighSensitivity_AllowsMissingPrefix()
    {
        Assert.True(WakeWordTextMatcher.IsMatch(
            "cojana",
            WakeWordPhrase.HeyKohana,
            WakeWordSensitivity.High));
    }

    [Fact]
    public void StrictSensitivity_RequiresBrandSpelling()
    {
        Assert.False(WakeWordTextMatcher.IsMatch(
            "ey cojana",
            WakeWordPhrase.HeyKohana,
            WakeWordSensitivity.Strict));
        Assert.True(WakeWordTextMatcher.IsMatch(
            "ey kohana",
            WakeWordPhrase.HeyKohana,
            WakeWordSensitivity.Strict));
    }

    [Fact]
    public void CustomAlias_IsAcceptedAndClassified()
    {
        var result = WakeWordTextMatcher.Evaluate(
            "coyana",
            WakeWordPhrase.HeyKohana,
            WakeWordSensitivity.Balanced,
            ["coyana"]);

        Assert.True(result.IsMatch);
        Assert.Equal(WakeWordMatchKind.CustomAlias, result.Kind);
    }

    [Fact]
    public void GrammarForKohana_DoesNotContainNexo()
    {
        var grammar = WakeWordTextMatcher.GetGrammarPhrases(
            WakeWordPhrase.HeyKohana,
            WakeWordSensitivity.High);

        Assert.Contains("ey cojana", grammar);
        Assert.DoesNotContain(grammar, phrase => phrase.Contains("nexo", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Nexo", WakeWordPhrase.Nexo)]
    [InlineData("oye nexo", WakeWordPhrase.OyeNexo)]
    [InlineData("hey nexo", WakeWordPhrase.HeyNexo)]
    public void LegacyModes_OnlyWorkWhenExplicitlySelected(
        string text,
        WakeWordPhrase phrase)
    {
        var result = WakeWordTextMatcher.Evaluate(text, phrase);

        Assert.True(result.IsMatch);
        Assert.Equal(WakeWordMatchKind.Legacy, result.Kind);
    }
}
