using Nexo.Core.Workspace;

namespace Nexo.Windows.Workspace;

/// <summary>
/// Diseño D14 (Fase 5, nivel 4) — escritura real en disco. **No decide nada**: los permisos, la
/// contención de rutas y el checkpoint son de <see cref="WorkspaceEditCoordinator"/>. Aquí solo se
/// escribe, se lee para el checkpoint y se borra al deshacer.
///
/// Aun así se comprueba la contención otra vez antes de tocar el disco. No es desconfianza del
/// coordinador: es que ésta es la última puerta antes de un archivo real, y una comprobación
/// duplicada cuesta microsegundos mientras que escribir fuera del proyecto no tiene arreglo.
/// </summary>
public sealed class FileSystemWorkspaceWriter : IWorkspaceWriter
{
    public (bool Existed, string Content) ReadForCheckpoint(string authorizedRoot, string relativePath)
    {
        if (!TryResolve(authorizedRoot, relativePath, out var fullPath))
        {
            return (false, string.Empty);
        }

        try
        {
            return File.Exists(fullPath)
                ? (true, File.ReadAllText(fullPath))
                : (false, string.Empty);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return (false, string.Empty);
        }
    }

    public WorkspaceStepResult WriteFile(string authorizedRoot, string relativePath, string content)
    {
        if (!TryResolve(authorizedRoot, relativePath, out var fullPath))
        {
            return WorkspaceStepResult.Failed(
                "Esa ruta cae fuera de la carpeta autorizada, así que no escribí nada.");
        }

        try
        {
            var directory = Path.GetDirectoryName(fullPath);

            // Solo se crean carpetas DENTRO de la raíz autorizada; TryResolve ya lo garantizó.
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Escritura atómica, igual que el resto de stores del proyecto: si Kohana muere a media
            // escritura, el archivo original sigue entero en vez de quedar truncado.
            var temporaryPath = fullPath + ".kohana-tmp";
            File.WriteAllText(temporaryPath, content);
            File.Move(temporaryPath, fullPath, overwrite: true);

            return WorkspaceStepResult.Ok($"Escribí {relativePath}.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return WorkspaceStepResult.Failed($"No pude escribir {relativePath}: {exception.Message}");
        }
    }

    public WorkspaceStepResult DeleteFile(string authorizedRoot, string relativePath)
    {
        if (!TryResolve(authorizedRoot, relativePath, out var fullPath))
        {
            return WorkspaceStepResult.Failed(
                "Esa ruta cae fuera de la carpeta autorizada, así que no borré nada.");
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return WorkspaceStepResult.Ok($"Borré {relativePath}.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return WorkspaceStepResult.Failed($"No pude borrar {relativePath}: {exception.Message}");
        }
    }

    private static bool TryResolve(string authorizedRoot, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(authorizedRoot) || string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        try
        {
            var candidate = Path.GetFullPath(Path.Combine(authorizedRoot, relativePath));
            if (!WorkspacePathPolicy.IsInside(candidate, authorizedRoot))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
