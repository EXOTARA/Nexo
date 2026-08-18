using System.Text.Json;

namespace Nexo.Core.Updates;

/// <summary>Lo que hace falta bajar de una publicación: el paquete y su huella.</summary>
public readonly record struct ReleaseAssets(
    bool IsUsable,
    string Version,
    string PackageUrl,
    long PackageSize,
    string ChecksumUrl,
    string Notes,
    string Problem)
{
    public static ReleaseAssets Rejected(string problem) =>
        new(false, string.Empty, string.Empty, 0, string.Empty, string.Empty, problem);
}

/// <summary>
/// Diseño D64 — lee una publicación de GitHub y saca de ella lo que hace falta.
///
/// Se usa GitHub y no un servidor propio porque **ya es donde se publica**: `publish.ps1` genera el
/// zip y su `.sha256` al lado, y una publicación de GitHub los lleva como adjuntos. Montar un
/// servicio aparte para anunciar versiones sería una pieza más que mantener, que se cae sola, y que
/// haría falta pagar.
///
/// La huella no viene en la respuesta de GitHub, así que se busca el adjunto `.sha256` que
/// acompaña al paquete. Que la huella viaje en un archivo aparte no la debilita: los dos van por
/// https desde el mismo sitio, y lo que se está protegiendo es una descarga que se corta o un
/// servidor que devuelve basura, no a GitHub mintiendo sobre su propio contenido.
///
/// **Aquí solo se lee.** Lo que se saca pasa después por
/// <see cref="UpdateManifestReader"/>, que es quien decide si vale. Separarlo importa: esto entiende
/// la forma de una respuesta de GitHub, y aquello entiende qué es aceptable — y lo segundo no debe
/// cambiar porque GitHub cambie su JSON.
/// </summary>
public static class GitHubReleaseReader
{
    /// <summary>La publicación más reciente del repositorio, sin contar borradores.</summary>
    public static Uri LatestReleaseUrl(string owner, string repository) =>
        new($"https://api.github.com/repos/{owner}/{repository}/releases/latest");

    public static ReleaseAssets Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ReleaseAssets.Rejected("La consulta de versiones no devolvió nada.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return ReleaseAssets.Rejected("La respuesta de versiones no tiene la forma esperada.");
            }

            // Un borrador no está publicado: ofrecerlo sería instalar algo que ni siquiera se ha
            // terminado de subir.
            if (root.TryGetProperty("draft", out var draft) &&
                draft.ValueKind == JsonValueKind.True)
            {
                return ReleaseAssets.Rejected("La última publicación es un borrador.");
            }

            var version = ReadString(root, "tag_name");
            if (version.Length == 0)
            {
                return ReleaseAssets.Rejected("La publicación no dice qué versión es.");
            }

            if (!root.TryGetProperty("assets", out var assets) ||
                assets.ValueKind != JsonValueKind.Array)
            {
                return ReleaseAssets.Rejected("La publicación no trae ningún archivo.");
            }

            var packageUrl = string.Empty;
            var checksumUrl = string.Empty;
            long packageSize = 0;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = ReadString(asset, "name");
                var url = ReadString(asset, "browser_download_url");

                if (name.Length == 0 || url.Length == 0)
                {
                    continue;
                }

                // El orden importa: «.zip.sha256» también termina en «.sha256», así que se mira
                // primero la huella. Al revés, la huella se tomaría por el paquete.
                if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
                {
                    checksumUrl = url;
                }
                else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    packageUrl = url;
                    packageSize = asset.TryGetProperty("size", out var size) &&
                                  size.TryGetInt64(out var bytes)
                        ? bytes
                        : 0;
                }
            }

            if (packageUrl.Length == 0)
            {
                return ReleaseAssets.Rejected("La publicación no trae el paquete de Kohana.");
            }

            if (checksumUrl.Length == 0)
            {
                // Sin huella no se instala. Una publicación a la que se le olvidó subir el .sha256
                // no es una publicación de la que fiarse.
                return ReleaseAssets.Rejected("La publicación no trae la huella para comprobarla.");
            }

            return new ReleaseAssets(
                IsUsable: true,
                Version: version,
                PackageUrl: packageUrl,
                PackageSize: packageSize,
                ChecksumUrl: checksumUrl,
                Notes: ReadString(root, "body"),
                Problem: string.Empty);
        }
        catch (JsonException)
        {
            // Una página de error, una redirección o un límite de peticiones llegan como algo que
            // no es JSON. Es un martes normal, no una excepción que deba subir.
            return ReleaseAssets.Rejected("La respuesta de versiones no se pudo entender.");
        }
    }

    /// <summary>
    /// Saca la huella del archivo <c>.sha256</c>.
    ///
    /// El formato habitual es «huella  nombre-del-archivo», que es lo que escriben tanto
    /// <c>sha256sum</c> como el script de publicación. Se toma la primera palabra y se ignora el
    /// resto: el nombre que venga detrás no aporta nada que no se sepa ya.
    /// </summary>
    public static string ReadChecksum(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var first = content
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim();

        if (string.IsNullOrEmpty(first))
        {
            return string.Empty;
        }

        var space = first.IndexOfAny([' ', '\t']);
        return space > 0 ? first[..space] : first;
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;
}
