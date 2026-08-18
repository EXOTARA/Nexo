using System.Globalization;

namespace Nexo.Core.Updates;

/// <summary>Una publicación anunciada: qué versión es, de dónde se baja y cómo comprobarla.</summary>
public sealed record UpdateManifest(
    KohanaVersion Version,
    Uri PackageUrl,
    string Sha256,
    long SizeInBytes,
    string Notes);

/// <summary>El anuncio leído, o por qué no se pudo usar.</summary>
public readonly record struct UpdateManifestResult(UpdateManifest? Manifest, string Problem)
{
    public bool IsUsable => Manifest is not null;

    public static UpdateManifestResult Ok(UpdateManifest manifest) => new(manifest, string.Empty);

    public static UpdateManifestResult Rejected(string problem) => new(null, problem);
}

/// <summary>
/// Diseño D62 — lee el anuncio de una versión publicada, y se niega a fiarse de él.
///
/// Todo lo que hay aquí llega de la red, así que la postura por defecto no es «leer» sino
/// «desconfiar». Un anuncio manipulado o simplemente equivocado termina en que Kohana descarga y
/// ejecuta algo que nadie publicó, y ese es el peor fallo posible de un actualizador: no rompe la
/// aplicación, la sustituye.
///
/// Por eso se rechaza en vez de apañarse:
///
/// **La dirección tiene que ser https.** Sobre http, cualquiera en el camino puede cambiar el
/// contenido, y comprobar la huella después no salva nada si quien la cambió también escribió la
/// huella.
///
/// **La huella es obligatoria y con la forma exacta.** Sin ella no hay forma de saber si lo que
/// llegó es lo que se publicó; con una de otra longitud, lo que hay es un campo que alguien
/// rellenó a mano y que no se va a poder comparar.
///
/// **El tamaño también.** Sirve para no empezar a bajar un archivo de tres gigas por un error de
/// publicación, y para notar antes de gastar tiempo que lo anunciado no cuadra.
/// </summary>
public static class UpdateManifestReader
{
    /// <summary>Una huella SHA-256 en hexadecimal ocupa exactamente esto.</summary>
    private const int Sha256HexLength = 64;

    /// <summary>
    /// Tope de lo que se admite anunciar. La edición autocontenida ronda los cien megas; medio giga
    /// deja sitio de sobra para que crezca y sigue descartando un anuncio absurdo.
    /// </summary>
    private const long MaximumPackageSize = 512L * 1024 * 1024;

    public static UpdateManifestResult Read(
        string? version,
        string? packageUrl,
        string? sha256,
        string? size,
        string? notes)
    {
        if (!KohanaVersion.TryParse(version, out var parsedVersion))
        {
            return UpdateManifestResult.Rejected("El anuncio no trae una versión que se pueda leer.");
        }

        if (string.IsNullOrWhiteSpace(packageUrl) ||
            !Uri.TryCreate(packageUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return UpdateManifestResult.Rejected("El anuncio no trae una dirección de descarga válida.");
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateManifestResult.Rejected(
                "La descarga tiene que ir por https: por http cualquiera en el camino puede cambiarla.");
        }

        var fingerprint = (sha256 ?? string.Empty).Trim();
        if (fingerprint.Length != Sha256HexLength || !IsHex(fingerprint))
        {
            return UpdateManifestResult.Rejected(
                "El anuncio no trae una huella SHA-256 con la forma correcta.");
        }

        if (!long.TryParse(size, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) ||
            bytes <= 0)
        {
            return UpdateManifestResult.Rejected("El anuncio no dice cuánto ocupa la descarga.");
        }

        if (bytes > MaximumPackageSize)
        {
            return UpdateManifestResult.Rejected(
                "La descarga anunciada es demasiado grande para ser una versión de Kohana.");
        }

        return UpdateManifestResult.Ok(new UpdateManifest(
            parsedVersion,
            uri,
            fingerprint.ToLowerInvariant(),
            bytes,
            (notes ?? string.Empty).Trim()));
    }

    private static bool IsHex(string value)
    {
        foreach (var character in value)
        {
            var isHex =
                character is >= '0' and <= '9' ||
                character is >= 'a' and <= 'f' ||
                character is >= 'A' and <= 'F';

            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compara la huella de lo descargado con la anunciada.
    ///
    /// Se compara sin distinguir mayúsculas porque las herramientas las escriben de las dos formas
    /// —PowerShell en mayúsculas, sha256sum en minúsculas— y una versión rechazada por eso sería un
    /// fallo imposible de entender desde fuera.
    /// </summary>
    public static bool MatchesFingerprint(string expected, string actual) =>
        expected.Trim().Equals(actual.Trim(), StringComparison.OrdinalIgnoreCase);
}
