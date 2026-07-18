using Nexo.Core.Commands;

namespace Nexo.Core.Tests;

public sealed class SpanishCommandLexiconTests
{
    [Theory]
    [InlineData("bajas spotify al 50", "baja spotify al 50")]
    [InlineData("bajale a spotify", "baja a spotify")]
    [InlineData("subes discord al 70", "sube discord al 70")]
    [InlineData("muestrame peek", "muestra peek")]
    [InlineData("ensename peek", "muestra peek")]
    [InlineData("por favor baja spotify", "baja spotify")]
    public void NormalizeForParsing_UsesControlledAliases(string text, string expected)
    {
        var result = SpanishCommandLexicon.NormalizeForParsing(text);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("vaja spotify", "baja spotify")]
    [InlineData("suba spotify", "sube spotify")]
    public void NormalizeForParsing_CorrectsOneLetterOnlyWhenCommandHasAnchor(
        string text,
        string expected)
    {
        var result = SpanishCommandLexicon.NormalizeForParsing(text);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeForParsing_DoesNotRewriteOpenQuestion()
    {
        var result = SpanishCommandLexicon.NormalizeForParsing(
            "por que bajas spotify cuando abro un juego");

        Assert.Equal("por que bajas spotify cuando abro un juego", result);
    }
}
