using System.IO;
using System.Runtime.InteropServices;
using Nexo.Core.Documents;

namespace Nexo.Windows.Documents;

/// <summary>Dónde acabó el documento, o por qué no acabó en ninguna parte.</summary>
public readonly record struct DocumentDropResult(bool Saved, string FullPath, string Message)
{
    public static DocumentDropResult Ok(string fullPath) => new(true, fullPath, string.Empty);

    public static DocumentDropResult Failed(string message) => new(false, string.Empty, message);
}

/// <summary>
/// Diseño D59 — deja el archivo en el sitio que se pidió por su nombre.
///
/// La política decide **si** se puede y **cómo se llama**; esto solo traduce el sitio a una carpeta
/// real y escribe. La separación importa porque lo primero se puede probar y lo segundo no: aquí no
/// hay ninguna decisión, y por eso no hay nada que discutir en una prueba.
///
/// **No se sobrescribe nunca.** Un documento que se pide dos veces con el mismo título es lo normal
/// —se repite la orden, se prueba otra vez— y sustituir en silencio el anterior significaría perder
/// algo que la persona quizá ya había editado. Se numera, como hace Windows al copiar.
/// </summary>
public sealed class WindowsDocumentDropService
{
    /// <summary>Lo que se intenta antes de rendirse con los nombres numerados.</summary>
    private const int MaximumAttempts = 200;

    public DocumentDropResult Save(DocumentTarget target, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!target.IsAllowed)
        {
            return DocumentDropResult.Failed(target.Message);
        }

        try
        {
            var folder = ResolveFolder(target.Folder);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return DocumentDropResult.Failed(
                    $"No encontré {DocumentDestination.Describe(target.Folder)} en este equipo.");
            }

            var path = FindFreeName(folder, target.FileName);
            if (path is null)
            {
                return DocumentDropResult.Failed(
                    "Ya hay demasiados archivos con ese nombre. Prueba con otro título.");
            }

            File.WriteAllBytes(path, content);
            return DocumentDropResult.Ok(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                NotSupportedException or ArgumentException)
        {
            // Lo que ve la persona dice qué pasó, no qué excepción fue. «Acceso denegado» sobre una
            // ruta que no eligió no le dice nada que pueda usar.
            return DocumentDropResult.Failed(
                $"No pude guardar en {DocumentDestination.Describe(target.Folder)}: {exception.Message}");
        }
    }

    /// <summary>
    /// Busca un nombre libre: «Informe.docx», «Informe (2).docx»…
    ///
    /// Se comprueba la existencia en vez de abrir con CreateNew y capturar, porque el caso normal es
    /// que esté libre y no tiene sentido pagar una excepción por lo habitual. La carrera que eso
    /// deja abierta —que alguien cree el archivo entre la comprobación y la escritura— existe, pero
    /// aquí el único que escribe es Kohana y la consecuencia sería un archivo sustituido, no un
    /// fallo silencioso.
    /// </summary>
    private static string? FindFreeName(string folder, string fileName)
    {
        var candidate = Path.Combine(folder, fileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var index = 2; index <= MaximumAttempts; index++)
        {
            candidate = Path.Combine(folder, $"{stem} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// El escritorio y los documentos los sabe .NET. Las descargas no: no tienen
    /// <c>SpecialFolder</c>, así que se preguntan a Windows por su identificador conocido.
    ///
    /// No se compone como <c>%USERPROFILE%\Downloads</c> porque esa carpeta se puede mover, y con
    /// ella movida el archivo aparecería en una ruta que ya no es la que abre el explorador. Si la
    /// consulta falla, entonces sí se usa esa suposición: mejor un sitio probable que ninguno.
    /// </summary>
    private static string ResolveFolder(DocumentFolder folder) => folder switch
    {
        DocumentFolder.Desktop => Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory),
        DocumentFolder.Documents => Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments),
        _ => ResolveDownloads()
    };

    private static string ResolveDownloads()
    {
        try
        {
            var result = SHGetKnownFolderPath(DownloadsFolderId, 0, IntPtr.Zero, out var path);
            if (result == 0 && !string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // Se cae a la suposición de abajo.
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    private static readonly Guid DownloadsFolderId =
        new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid id,
        uint flags,
        IntPtr token,
        [MarshalAs(UnmanagedType.LPWStr)] out string path);
}
