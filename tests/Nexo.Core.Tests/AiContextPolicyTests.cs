using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class AiContextPolicyTests
{
    [Theory]
    [InlineData("¿Por qué mi PC podría estar lenta?")]
    [InlineData("¿Cuánta RAM estoy usando?")]
    [InlineData("¿Cómo anda mi GPU ahora mismo?")]
    [InlineData("¿Qué proceso está usando más memoria en mi equipo?")]
    public void ShouldIncludeSystemMetrics_ReturnsTrueForCurrentDeviceQuestions(string prompt)
    {
        Assert.True(AiContextPolicy.ShouldIncludeSystemMetrics(prompt));
    }

    [Theory]
    [InlineData("¿Qué día es hoy?")]
    [InlineData("Explícame qué es la memoria RAM")]
    [InlineData("¿Por qué una computadora puede sentirse lenta aunque la CPU esté al 20 %?")]
    [InlineData("Escribe una descripción corta")]
    public void ShouldIncludeSystemMetrics_ReturnsFalseForUnrelatedOrGeneralQuestions(string prompt)
    {
        Assert.False(AiContextPolicy.ShouldIncludeSystemMetrics(prompt));
    }
}
