using Nexo.Core.Flow;

namespace Nexo.Core.Tests;

public sealed class FlowSettingsParserTests
{
    [Fact]
    public void ParseDictionary_WithNull_ReturnsEmpty()
    {
        Assert.Empty(FlowSettingsParser.ParseDictionary(null));
    }

    [Fact]
    public void ParseDictionary_ReadsSpokenAndWritten()
    {
        var entries = FlowSettingsParser.ParseDictionary(["cojana=Kohana"]);

        var entry = Assert.Single(entries);
        Assert.Equal("cojana", entry.Spoken);
        Assert.Equal("Kohana", entry.Replacement);
    }

    [Fact]
    public void ParseDictionary_TrimsSurroundingSpaces()
    {
        var entry = Assert.Single(FlowSettingsParser.ParseDictionary(["  cojana  =  Kohana  "]));

        Assert.Equal("cojana", entry.Spoken);
        Assert.Equal("Kohana", entry.Replacement);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin separador")]
    [InlineData("=solo lo escrito")]
    [InlineData("solo lo dicho=")]
    public void ParseDictionary_SkipsMalformedLinesInsteadOfFailing(string line)
    {
        // El archivo de ajustes se puede editar a mano: una línea mal escrita no debe tumbar
        // el resto del diccionario.
        Assert.Empty(FlowSettingsParser.ParseDictionary([line]));
    }

    [Fact]
    public void ParseDictionary_KeepsTheGoodLinesWhenOthersAreBroken()
    {
        var entries = FlowSettingsParser.ParseDictionary(["roto", "cojana=Kohana", ""]);

        Assert.Single(entries);
    }

    [Fact]
    public void ParseSnippets_SplitsOnlyOnTheFirstEqualsSign()
    {
        // Una expansión puede contener "=" y debe conservarlo íntegro.
        var snippet = Assert.Single(FlowSettingsParser.ParseSnippets(["asigna=total = suma"]));

        Assert.Equal("asigna", snippet.Trigger);
        Assert.Equal("total = suma", snippet.Expansion);
    }
}
