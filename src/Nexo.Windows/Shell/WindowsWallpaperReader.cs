using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Nexo.Core.Shell;

namespace Nexo.Windows.Shell;

/// <summary>
/// Diseño D31 — lee el fondo de escritorio y devuelve sus píxeles para que
/// <see cref="WallpaperPalette"/> saque la paleta.
///
/// La imagen se decodifica reducida a <see cref="SampleWidth"/> píxeles de ancho y no a tamaño
/// completo. No es un ahorro cosmético: un fondo 4K son ocho millones de píxeles, y cargarlo entero
/// cada vez que alguien cambia de fondo costaría decenas de megabytes y un tirón visible en la
/// interfaz. Para saber de qué color es una imagen, una versión pequeña dice exactamente lo mismo
/// que la grande.
///
/// Se prefiere TranscodedWallpaper —la copia que Windows genera y muestra de verdad— sobre la ruta
/// del registro, porque esa ruta apunta al archivo original: si se movió, se borró o está en un
/// disco desconectado, deja de existir, y con presentación de diapositivas se queda desfasada.
/// </summary>
public static class WindowsWallpaperReader
{
    /// <summary>Ancho al que se reduce la imagen antes de muestrearla.</summary>
    private const int SampleWidth = 160;

    public static IReadOnlyList<RgbColor>? TryReadPixels()
    {
        foreach (var path in CandidatePaths())
        {
            var pixels = TryDecode(path);
            if (pixels is not null)
            {
                return pixels;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidatePaths()
    {
        // Diseño D32 — el fondo animado va PRIMERO. Con Wallpaper Engine puesto, el registro y
        // TranscodedWallpaper siguen apuntando a la última imagen que puso Windows: preguntar por
        // ellos primero daría el color de un fondo que ya nadie está viendo.
        if (LiveWallpaperReader.TryFindPreviewImage() is { } livePreview)
        {
            yield return livePreview;
        }

        var transcoded = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Themes", "TranscodedWallpaper");

        if (File.Exists(transcoded))
        {
            yield return transcoded;
        }

        string? registryPath = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            registryPath = key?.GetValue("WallPaper") as string;
        }
        catch (Exception exception) when (
            exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Sin acceso al registro queda TranscodedWallpaper, que es la fuente preferida de todos modos.
        }

        if (!string.IsNullOrWhiteSpace(registryPath) && File.Exists(registryPath))
        {
            yield return registryPath;
        }
    }

    private static IReadOnlyList<RgbColor>? TryDecode(string path)
    {
        try
        {
            // El archivo se abre por nuestra cuenta y con FileShare.Read: Windows puede estar
            // leyendo TranscodedWallpaper al mismo tiempo, y dejar que BitmapImage abra la ruta por
            // su cuenta la mantendría bloqueada más tiempo del necesario.
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = SampleWidth;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            converted.Freeze();

            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var stride = width * 4;
            var buffer = new byte[stride * height];
            converted.CopyPixels(buffer, stride, 0);

            var pixels = new List<RgbColor>(width * height);
            for (var offset = 0; offset < buffer.Length; offset += 4)
            {
                // Bgra32: el orden en memoria es azul, verde, rojo, alfa. Los píxeles casi
                // transparentes no aportan color visible al escritorio y se descartan.
                if (buffer[offset + 3] < 8)
                {
                    continue;
                }

                pixels.Add(new RgbColor(buffer[offset + 2], buffer[offset + 1], buffer[offset]));
            }

            return pixels.Count > 0 ? pixels : null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException
                or ArgumentException or OverflowException or FileFormatException)
        {
            // Un fondo en un formato que WPF no decodifica, un archivo a medio escribir o un disco
            // desconectado no deben impedir que Kohana abra: se prueba el siguiente candidato.
            return null;
        }
    }
}
