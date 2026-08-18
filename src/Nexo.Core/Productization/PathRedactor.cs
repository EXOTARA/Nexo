using System.Text.RegularExpressions;

namespace Nexo.Core.Productization;

/// <summary>
/// Diseño D21 — sustituye la carpeta del usuario por su variable de entorno en cualquier ruta.
///
/// Existe por una fuga pequeña y muy fácil de pasar por alto: un paquete de soporte lleno de rutas
/// como <c>C:\Users\adler\AppData\...</c> revela el nombre real de la persona en cada línea, y el
/// paquete se manda a alguien. No es el dato más sensible del mundo, pero es un dato personal que se
/// cuela sin que nadie lo haya decidido, y esos son los que más veces se escapan.
/// </summary>
public static partial class PathRedactor
{
    public static string Shorten(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var shortened = path;

        if (!string.IsNullOrWhiteSpace(profile))
        {
            shortened = shortened.Replace(profile, "%UserProfile%", StringComparison.OrdinalIgnoreCase);
        }

        // Y por si el texto viene de otro sitio (un log, una excepción) con una ruta de usuario que
        // no es la del perfil actual: el patrón la reconoce igual.
        return UserFolderPattern().Replace(shortened, @"C:\Users\%usuario%");
    }

    [GeneratedRegex(@"(?i)[A-Z]:\\Users\\[^\\/:*?""<>|\r\n]+", RegexOptions.Compiled)]
    private static partial Regex UserFolderPattern();
}
