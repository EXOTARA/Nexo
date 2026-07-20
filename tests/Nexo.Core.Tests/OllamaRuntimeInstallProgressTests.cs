using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class OllamaRuntimeInstallProgressTests
{
    [Fact]
    public void Percentage_IsCalculatedFromByteCounts()
    {
        var progress = new OllamaRuntimeInstallProgress(
            "download",
            "Descargando…",
            25,
            100);

        Assert.Equal(25d, progress.Percentage);
    }

    [Theory]
    [InlineData(150, 100, 100)]
    [InlineData(-10, 100, 0)]
    public void Percentage_IsClamped(
        long completed,
        long total,
        double expected)
    {
        var progress = new OllamaRuntimeInstallProgress(
            "download",
            "Descargando…",
            completed,
            total);

        Assert.Equal(expected, progress.Percentage);
    }

    [Fact]
    public void Percentage_IsNullWithoutKnownTotal()
    {
        var progress = new OllamaRuntimeInstallProgress(
            "download",
            "Descargando…",
            10,
            null);

        Assert.Null(progress.Percentage);
    }
}
