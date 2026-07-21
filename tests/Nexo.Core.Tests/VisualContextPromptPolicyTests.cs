using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

public sealed class VisualContextPromptPolicyTests
{
    [Theory]
    [InlineData("¿Qué es esto?")]
    [InlineData("¿Qué es este problema?")]
    [InlineData("¿Por qué falla esto?")]
    [InlineData("Mira esto y dime qué significa")]
    [InlineData("¿Qué estoy viendo?")]
    public void VoiceDeicticQuestion_AcquiresVisualContext(string prompt)
    {
        Assert.True(VisualContextPromptPolicy.ShouldAcquireVisualContext(
            prompt,
            fromVoice: true));
    }

    [Fact]
    public void TypedQuestion_DoesNotCaptureSilently()
    {
        Assert.False(VisualContextPromptPolicy.ShouldAcquireVisualContext(
            "¿Qué es esto?",
            fromVoice: false));
    }

    [Fact]
    public void UnrelatedVoicePrompt_DoesNotCapture()
    {
        Assert.False(VisualContextPromptPolicy.ShouldAcquireVisualContext(
            "abre descargas",
            fromVoice: true));
    }
}
