using Nexo.Core.Ai;
using Nexo.Core.Resources;

namespace Nexo.Core.Tests;

public sealed class AiExecutionLocationPolicyTests
{
    [Theory]
    [InlineData(AiProviderKind.Ollama, "http://localhost:11435/v1")]
    [InlineData(AiProviderKind.LMStudio, "http://localhost:1234/v1")]
    [InlineData(AiProviderKind.OpenAICompatible, "http://127.0.0.1:8000/v1")]
    public void LocalProviders_AreRecognized(AiProviderKind provider, string baseUrl)
    {
        var configuration = new AiProviderConfiguration(provider, baseUrl, "model", "KEY");

        Assert.True(AiExecutionLocationPolicy.UsesLocalRuntime(configuration));
    }

    [Theory]
    [InlineData(AiProviderKind.OpenAI, "https://api.openai.com/v1")]
    [InlineData(AiProviderKind.OpenAICompatible, "https://example.com/v1")]
    public void RemoteProviders_AreNotMarkedLocal(AiProviderKind provider, string baseUrl)
    {
        var configuration = new AiProviderConfiguration(provider, baseUrl, "model", "KEY");

        Assert.False(AiExecutionLocationPolicy.UsesLocalRuntime(configuration));
    }
}
