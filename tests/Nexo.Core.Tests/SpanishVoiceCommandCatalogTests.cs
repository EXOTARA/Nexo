using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class SpanishVoiceCommandCatalogTests
{
    [Fact]
    public void Catalog_ContainsCoreSystemCommands()
    {
        var phrases = SpanishVoiceCommandCatalog.CreatePhrases();

        Assert.Contains("cómo está mi pc", phrases);
        Assert.Contains("muestra peek", phrases);
        Assert.Contains("abre powershell", phrases);
    }

    [Fact]
    public void Catalog_ContainsAudioCommandWithWakeAliases()
    {
        var phrases = SpanishVoiceCommandCatalog.CreatePhrases();

        Assert.Contains("pon discord al veinticinco por ciento", phrases);
        Assert.Contains("nexo pon discord al veinticinco por ciento", phrases);
        Assert.Contains("exo pon discord al veinticinco por ciento", phrases);
    }

    [Fact]
    public void Catalog_DoesNotContainDuplicatePhrases()
    {
        var phrases = SpanishVoiceCommandCatalog.CreatePhrases();

        Assert.Equal(
            phrases.Count,
            phrases.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
