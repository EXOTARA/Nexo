using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class OllamaRuntimeEndpointsTests
{
    [Theory]
    [InlineData("http://localhost:11435/v1")]
    [InlineData("http://localhost:11435/v1/")]
    [InlineData("http://127.0.0.1:11435/v1")]
    [InlineData("http://[::1]:11435/v1")]
    [InlineData("http://localhost:11435")]
    public void ManagedUrls_AreRecognized(string baseUrl)
    {
        Assert.True(OllamaRuntimeEndpoints.IsManagedBaseUrl(baseUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("http://localhost:11435/api")]
    [InlineData("not-a-url")]
    public void OtherUrls_AreNotManaged(string? baseUrl)
    {
        Assert.False(OllamaRuntimeEndpoints.IsManagedBaseUrl(baseUrl));
    }
}
