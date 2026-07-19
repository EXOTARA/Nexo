using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class OllamaModelNameTests
{
    [Theory]
    [InlineData(" qwen3.5:9b ", "qwen3.5:9b")]
    [InlineData("library/model_name:latest", "library/model_name:latest")]
    [InlineData("gemma3:4b", "gemma3:4b")]
    public void Normalize_AcceptsSafeModelNames(string input, string expected)
    {
        Assert.Equal(expected, OllamaModelName.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("modelo con espacios")]
    [InlineData("modelo; rm -rf")]
    public void Normalize_RejectsUnsafeModelNames(string input)
    {
        Assert.Null(OllamaModelName.Normalize(input));
    }
}
