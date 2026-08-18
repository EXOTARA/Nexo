using System.Net.NetworkInformation;
using Nexo.Core.Metrics;

namespace Nexo.Windows.Metrics;

/// <summary>Espacio de una unidad, ya en bytes reales y no en porcentaje.</summary>
public readonly record struct DriveSpace(string Name, long UsedBytes, long TotalBytes);

/// <summary>
/// Diseño D42 — velocidad de red y espacio de disco, los dos datos del boceto de rendimiento que
/// Kohana no tenía.
///
/// La red se lee sumando los contadores de todas las interfaces activas y no de una sola: en un
/// portátil con Wi-Fi y cable, mirar solo la primera da cero en cuanto se cambia de una a otra. Se
/// excluyen los bucles locales, que contabilizan tráfico que nunca sale del equipo.
///
/// No incluye temperatura. Windows no la expone por una vía normal: hace falta un controlador en modo
/// núcleo (lo que instalan las herramientas del tipo de HWMonitor) y permisos de administrador. Se
/// deja fuera a propósito en vez de enseñar un hueco o, peor, un número inventado.
/// </summary>
public sealed class WindowsThroughputSource
{
    private long _lastDownloaded;
    private long _lastUploaded;
    private DateTimeOffset _lastRead = DateTimeOffset.MinValue;

    public NetworkThroughput ReadNetwork()
    {
        long downloaded = 0;
        long uploaded = 0;

        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var statistics = adapter.GetIPStatistics();
                downloaded += statistics.BytesReceived;
                uploaded += statistics.BytesSent;
            }
        }
        catch (Exception exception) when (
            exception is NetworkInformationException or PlatformNotSupportedException)
        {
            return new NetworkThroughput(0, 0);
        }

        var now = DateTimeOffset.UtcNow;

        // La primera lectura no tiene con qué compararse: se guarda como base y se informa cero. Sin
        // esto, el primer valor sería «todo lo que ha movido la tarjeta desde que arrancó el equipo
        // partido por un instante», es decir, una cifra enorme y falsa.
        if (_lastRead == DateTimeOffset.MinValue)
        {
            _lastDownloaded = downloaded;
            _lastUploaded = uploaded;
            _lastRead = now;
            return new NetworkThroughput(0, 0);
        }

        var throughput = NetworkRate.Compute(
            downloaded, uploaded, _lastDownloaded, _lastUploaded, now - _lastRead);

        // La base solo se mueve si de verdad se calculó algo: si dos lecturas caen demasiado juntas,
        // conservar la anterior permite que la siguiente tenga un intervalo utilizable.
        if (now - _lastRead >= NetworkRate.MinimumInterval)
        {
            _lastDownloaded = downloaded;
            _lastUploaded = uploaded;
            _lastRead = now;
        }

        return throughput;
    }

    /// <summary>
    /// Espacio de la unidad del sistema. Se informa en bytes y no solo en porcentaje porque «21%»
    /// no dice si quedan cinco gigas o quinientos, que es lo que alguien necesita saber.
    /// </summary>
    public DriveSpace? ReadSystemDrive()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
            {
                return null;
            }

            return new DriveSpace(
                drive.Name.TrimEnd(Path.DirectorySeparatorChar),
                drive.TotalSize - drive.AvailableFreeSpace,
                drive.TotalSize);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
