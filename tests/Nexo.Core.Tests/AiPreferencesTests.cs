using Nexo.Core.Ai;
using Nexo.Core.Settings;

namespace Nexo.Core.Tests;

public sealed class AiPreferencesTests
{
    [Fact]
    public void Normalize_MigratesAiAsDisabledAndPrivateByDefault()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 5,
            AiProvider = AiProviderKind.OpenAI,
            AiBaseUrl = "https://example.invalid/v1",
            AiModel = "secret-model",
            ShareSystemMetricsWithAi = true
        };

        preferences.Normalize();

        Assert.Equal(16, preferences.SchemaVersion);
        Assert.Equal(AiProviderKind.Disabled, preferences.AiProvider);
        Assert.Equal(string.Empty, preferences.AiBaseUrl);
        Assert.Equal(string.Empty, preferences.AiModel);
        Assert.Equal("OPENAI_API_KEY", preferences.AiApiKeyEnvironmentVariable);
        Assert.False(preferences.ShareSystemMetricsWithAi);
    }

    [Fact]
    public void Normalize_PreservesCurrentOpenAiConfiguration()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 6,
            AiProvider = AiProviderKind.OpenAI,
            AiBaseUrl = " https://api.openai.com/v1/ ",
            AiModel = " gpt-5-mini ",
            AiApiKeyEnvironmentVariable = " OPENAI_API_KEY ",
            ShareSystemMetricsWithAi = true
        };

        preferences.Normalize();

        Assert.Equal(AiProviderKind.OpenAI, preferences.AiProvider);
        Assert.Equal("https://api.openai.com/v1", preferences.AiBaseUrl);
        Assert.Equal("gpt-5-mini", preferences.AiModel);
        Assert.Equal("OPENAI_API_KEY", preferences.AiApiKeyEnvironmentVariable);
        Assert.True(preferences.ShareSystemMetricsWithAi);
    }

    [Fact]
    public void Normalize_RepairsUnknownProvider()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 6,
            AiProvider = (AiProviderKind)999
        };

        preferences.Normalize();

        Assert.Equal(AiProviderKind.Disabled, preferences.AiProvider);
    }

    [Fact]
    public void Normalize_FillsOpenAiDefaultsWhenFieldsAreEmpty()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 6,
            AiProvider = AiProviderKind.OpenAI,
            AiBaseUrl = " ",
            AiModel = " ",
            AiApiKeyEnvironmentVariable = " "
        };

        preferences.Normalize();

        Assert.Equal("https://api.openai.com/v1", preferences.AiBaseUrl);
        Assert.Equal("gpt-5-mini", preferences.AiModel);
        Assert.Equal("OPENAI_API_KEY", preferences.AiApiKeyEnvironmentVariable);
    }
}
