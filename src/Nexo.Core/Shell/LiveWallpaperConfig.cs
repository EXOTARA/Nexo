using System.Text.Json;

namespace Nexo.Core.Shell;

/// <summary>
/// Diseño D32 — encuentra qué fondo tiene puesto Wallpaper Engine ahora mismo.
///
/// Existe porque el color de Kohana no cambiaba nunca con Wallpaper Engine, y la causa no era la
/// lectura sino que <b>no hay nada que leer</b>: ese programa no cambia el fondo de Windows, dibuja
/// en vivo sobre la capa del escritorio y deja el registro y <c>TranscodedWallpaper</c> apuntando a
/// la última imagen que puso Windows.
///
/// Antes de llegar aquí se intentó capturar directamente lo dibujado en el escritorio, que habría
/// servido para cualquier programa de fondos animados. No funciona en Windows 11: el fondo lo
/// compone DWM fuera de GDI, así que un volcado normal sale en negro y
/// <c>PrintWindow</c> —incluso pidiendo contenido compuesto— devuelve la capa de iconos, no el
/// fondo. Se comprobó en pantalla antes de descartarlo.
///
/// Lo que sí funciona: Wallpaper Engine anota en su <c>config.json</c> el fondo elegido por monitor,
/// y cada fondo trae al lado un <c>preview.jpg</c> —la miniatura que se ve en su biblioteca—. Para
/// saber de qué color es un fondo, esa miniatura vale igual que el fondo animado.
///
/// Aquí vive solo la lectura del JSON, sin tocar disco, para poder probarla sin tener Wallpaper
/// Engine instalado.
/// </summary>
public static class LiveWallpaperConfig
{
    /// <summary>
    /// Saca las rutas de fondo declaradas en el <c>config.json</c> de Wallpaper Engine, en el orden
    /// en que aparecen.
    ///
    /// La estructura es <c>{usuario}.general.wallpaperconfig.selectedwallpapers.{Monitor}.file</c>,
    /// y el primer nivel es el nombre de la cuenta de Windows, que obviamente no se puede escribir
    /// en el código. Por eso se recorre buscando la forma en vez de navegar por una ruta fija: así
    /// da igual cómo se llame la cuenta y cuántos monitores haya.
    /// </summary>
    public static IReadOnlyList<string> ReadSelectedWallpapers(string configJson)
    {
        ArgumentNullException.ThrowIfNull(configJson);

        var results = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(configJson);
            Collect(document.RootElement, results, depth: 0);
        }
        catch (JsonException)
        {
            // Un config a medio escribir no debe impedir que Kohana abra: se queda sin este origen.
        }

        return results;
    }

    private static void Collect(JsonElement element, List<string> results, int depth)
    {
        // Un tope de profundidad evita que un config inesperadamente anidado —o manipulado— haga
        // recorrer el árbol indefinidamente. La ruta real está a cinco niveles.
        if (depth > 8 || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("selectedwallpapers", out var selected) &&
            selected.ValueKind == JsonValueKind.Object)
        {
            foreach (var monitor in selected.EnumerateObject())
            {
                if (monitor.Value.ValueKind == JsonValueKind.Object &&
                    monitor.Value.TryGetProperty("file", out var file) &&
                    file.ValueKind == JsonValueKind.String &&
                    file.GetString() is { Length: > 0 } path)
                {
                    results.Add(path);
                }
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            Collect(property.Value, results, depth + 1);
        }
    }

    /// <summary>
    /// Convierte la ruta del fondo en la de su miniatura. El valor apunta al archivo del fondo
    /// —<c>scene.pkg</c> para uno animado, o un vídeo o imagen— y al lado siempre hay un
    /// <c>preview</c> que es lo que se puede leer como imagen en todos los casos.
    /// </summary>
    public static string? PreviewFolderOf(string wallpaperFilePath)
    {
        if (string.IsNullOrWhiteSpace(wallpaperFilePath))
        {
            return null;
        }

        var normalized = wallpaperFilePath.Replace('\\', '/').TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');

        return lastSlash <= 0 ? null : normalized[..lastSlash];
    }

    /// <summary>Nombres de miniatura que usa Wallpaper Engine, en orden de preferencia.</summary>
    public static IReadOnlyList<string> PreviewFileNames { get; } =
    [
        "preview.jpg",
        "preview.png",
        "preview.gif",
        "preview.jpeg"
    ];
}
