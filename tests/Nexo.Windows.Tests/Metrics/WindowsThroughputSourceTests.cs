using Nexo.Windows.Metrics;
using Xunit;

namespace Nexo.Windows.Tests.Metrics;

/// <summary>
/// Corre contra la red y el disco reales. Es deliberado: lo que hay que comprobar es que los
/// contadores del sistema se lean y se conviertan bien, y eso un doble no lo puede decir.
/// </summary>
public sealed class WindowsThroughputSourceTests
{
    [Fact]
    public void TheFirstNetworkReadingIsZeroInsteadOfEverythingSinceBoot()
    {
        // Sin base con la que comparar, la única respuesta honesta es cero. Si informara la
        // diferencia contra cero, el primer valor sería todo lo que ha movido la tarjeta desde que
        // arrancó el equipo: una cifra enorme y falsa, justo al abrir el panel.
        var source = new WindowsThroughputSource();

        var first = source.ReadNetwork();

        Assert.Equal(0, first.DownloadBytesPerSecond);
        Assert.Equal(0, first.UploadBytesPerSecond);
    }

    [Fact]
    public async Task ASecondReadingProducesANonNegativeRate()
    {
        var source = new WindowsThroughputSource();
        source.ReadNetwork();

        await Task.Delay(600);
        var second = source.ReadNetwork();

        Assert.True(second.DownloadBytesPerSecond >= 0);
        Assert.True(second.UploadBytesPerSecond >= 0);
        Assert.False(double.IsInfinity(second.DownloadBytesPerSecond));
        Assert.False(double.IsNaN(second.DownloadBytesPerSecond));
    }

    [Fact]
    public void ReadingsTakenBackToBackNeverBlowUp()
    {
        // Dos llamadas seguidas caen dentro del intervalo mínimo. Lo que no puede pasar es que salga
        // un infinito por dividir entre casi nada.
        var source = new WindowsThroughputSource();

        for (var i = 0; i < 20; i++)
        {
            var rate = source.ReadNetwork();
            Assert.False(double.IsInfinity(rate.DownloadBytesPerSecond));
            Assert.False(double.IsNaN(rate.DownloadBytesPerSecond));
            Assert.True(rate.DownloadBytesPerSecond >= 0);
        }
    }

    [Fact]
    public void TheSystemDriveReportsCoherentSpace()
    {
        var source = new WindowsThroughputSource();

        var drive = source.ReadSystemDrive();

        Assert.NotNull(drive);
        Assert.True(drive!.Value.TotalBytes > 0, "El disco del sistema debería tener tamaño.");
        Assert.InRange(drive.Value.UsedBytes, 0, drive.Value.TotalBytes);
        Assert.False(string.IsNullOrWhiteSpace(drive.Value.Name));
    }
}
