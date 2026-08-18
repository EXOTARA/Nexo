using Nexo.Core.Metrics;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class NetworkRateTests
{
    [Fact]
    public void TheRateIsTheDifferenceOverTheElapsedTime()
    {
        var rate = NetworkRate.Compute(
            downloadedBytesNow: 3_000_000,
            uploadedBytesNow: 500_000,
            downloadedBytesBefore: 1_000_000,
            uploadedBytesBefore: 400_000,
            elapsed: TimeSpan.FromSeconds(2));

        Assert.Equal(1_000_000, rate.DownloadBytesPerSecond, 1);
        Assert.Equal(50_000, rate.UploadBytesPerSecond, 1);
    }

    [Fact]
    public void ACounterThatWentBackwardsReportsZeroInsteadOfANegativeSpeed()
    {
        // Pasa de verdad: al reconectar la red, al despertar el equipo o al desactivar una interfaz,
        // el contador acumulado se reinicia. Una velocidad de descarga negativa no significa nada.
        var rate = NetworkRate.Compute(
            downloadedBytesNow: 100,
            uploadedBytesNow: 50,
            downloadedBytesBefore: 9_000_000,
            uploadedBytesBefore: 8_000_000,
            elapsed: TimeSpan.FromSeconds(2));

        Assert.Equal(0, rate.DownloadBytesPerSecond);
        Assert.Equal(0, rate.UploadBytesPerSecond);
    }

    [Fact]
    public void TwoReadingsTakenTooCloseTogetherReportNothing()
    {
        // Sin este suelo, dividir por un intervalo diminuto da un número disparatado que aparece un
        // instante en pantalla y desaparece: exactamente el tipo de parpadeo que nadie consigue
        // reproducir después.
        var rate = NetworkRate.Compute(
            downloadedBytesNow: 2_000_000,
            uploadedBytesNow: 1_000_000,
            downloadedBytesBefore: 1_000_000,
            uploadedBytesBefore: 500_000,
            elapsed: TimeSpan.FromMilliseconds(5));

        Assert.Equal(0, rate.DownloadBytesPerSecond);
        Assert.Equal(0, rate.UploadBytesPerSecond);
    }

    [Fact]
    public void AZeroIntervalNeverProducesInfinity()
    {
        var rate = NetworkRate.Compute(1_000, 1_000, 0, 0, TimeSpan.Zero);

        Assert.False(double.IsInfinity(rate.DownloadBytesPerSecond));
        Assert.False(double.IsNaN(rate.DownloadBytesPerSecond));
        Assert.Equal(0, rate.DownloadBytesPerSecond);
    }

    [Fact]
    public void AnIdleNetworkReportsZeroRatherThanNothing()
    {
        // Sin tráfico, los contadores no cambian: la velocidad es cero, y eso es un dato, no un
        // hueco.
        var rate = NetworkRate.Compute(5_000, 5_000, 5_000, 5_000, TimeSpan.FromSeconds(2));

        Assert.Equal(0, rate.DownloadBytesPerSecond);
        Assert.Equal(0, rate.UploadBytesPerSecond);
    }
}
