using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class AiProviderDefaultsTests
{
    [Fact]
    public void OpenAi_UsesOfficialBaseUrlAndEnvironmentVariable()
    {
        var preset = AiProviderDefaults.Get(AiProviderKind.OpenAI);

        Assert.Equal("https://api.openai.com/v1", preset.BaseUrl);
        Assert.Equal("gpt-5-mini", preset.DefaultModel);
        Assert.Equal("OPENAI_API_KEY", preset.ApiKeyEnvironmentVariable);
        Assert.True(preset.RequiresApiKey);
    }

    [Fact]
    public void LocalProviders_UseLocalOpenAiCompatibleEndpoints()
    {
        var ollama = AiProviderDefaults.Get(AiProviderKind.Ollama);
        var lmStudio = AiProviderDefaults.Get(AiProviderKind.LMStudio);

        Assert.Equal("http://localhost:11434/v1", ollama.BaseUrl);
        Assert.Equal("http://localhost:1234/v1", lmStudio.BaseUrl);
        Assert.False(ollama.RequiresApiKey);
        Assert.False(lmStudio.RequiresApiKey);
    }

    [Theory]
    [InlineData("http://localhost:1234/v1/", "http://localhost:1234/v1")]
    [InlineData("  https://api.openai.com/v1  ", "https://api.openai.com/v1")]
    [InlineData(null, "")]
    public void NormalizeBaseUrl_TrimsWhitespaceAndTrailingSlash(
        string? input,
        string expected)
    {
        Assert.Equal(expected, AiProviderDefaults.NormalizeBaseUrl(input));
    }
}
