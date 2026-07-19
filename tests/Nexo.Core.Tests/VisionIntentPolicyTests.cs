using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class VisionIntentPolicyTests
{
    [Theory]
    [InlineData("¿Qué significa este error?")]
    [InlineData("Explícame por qué no compila")]
    [InlineData("Lee el código CS0266 y dame la corrección")]
    [InlineData("¿Qué falla aparece en la terminal?")]
    [InlineData("Revisa el archivo y la línea indicada")]
    public void Resolve_ReturnsTechnicalDiagnosticForTechnicalVisionPrompts(string prompt)
    {
        Assert.Equal(
            AiRequestMode.VisionTechnicalDiagnostic,
            VisionIntentPolicy.Resolve(prompt, hasImages: true));
    }

    [Theory]
    [InlineData("Resume esta ventana")]
    [InlineData("¿Qué ves en la imagen?")]
    public void Resolve_ReturnsGeneralVisionForNonTechnicalImageQuestions(string prompt)
    {
        Assert.Equal(
            AiRequestMode.VisionGeneral,
            VisionIntentPolicy.Resolve(prompt, hasImages: true));
    }

    [Fact]
    public void Resolve_ReturnsStandardWithoutImages()
    {
        Assert.Equal(
            AiRequestMode.Standard,
            VisionIntentPolicy.Resolve("¿Qué significa este error?", hasImages: false));
    }
}
