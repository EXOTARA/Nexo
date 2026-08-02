using Nexo.Core.Workspace;

namespace Nexo.Windows.Workspace;

/// <summary>
/// Diseño D12 (Fase 5) — lectura real del proyecto autorizado. Cada archivo pasa por
/// <see cref="WorkspacePathPolicy"/> ANTES de abrirse; esta clase no decide nada por su cuenta, solo
/// ejecuta lo que la política permite. Es a propósito: si la decisión viviera aquí, cada nueva forma
/// de listar o buscar podría inventarse sus propias reglas.
///
/// El recorrido no sigue enlaces simbólicos ni uniones de directorio. En Windows una unión dentro
/// del proyecto puede apuntar a cualquier parte del disco, y seguirla convertiría "autoricé esta
/// carpeta" en "autoricé lo que esa carpeta señale".
/// </summary>
public sealed class FileSystemWorkspaceReader : IWorkspaceReader
{
    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden
    };

    public IReadOnlyList<WorkspaceFile> ListFiles(string authorizedRoot, int maximumFiles)
    {
        if (!Directory.Exists(authorizedRoot) || maximumFiles <= 0)
        {
            return [];
        }

        var files = new List<WorkspaceFile>();

        foreach (var path in EnumerateSafely(authorizedRoot))
        {
            if (!WorkspacePathPolicy.CanRead(path, authorizedRoot).IsAllowed)
            {
                continue;
            }

            long length;
            try
            {
                length = new FileInfo(path).Length;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (!WorkspacePathPolicy.CheckSize(length).IsAllowed)
            {
                continue;
            }

            files.Add(new WorkspaceFile(path, Path.GetRelativePath(authorizedRoot, path), length));

            if (files.Count >= maximumFiles)
            {
                break;
            }
        }

        return files;
    }

    public WorkspaceReadResult ReadFile(string authorizedRoot, string fullPath)
    {
        var relative = SafeRelativePath(authorizedRoot, fullPath);

        var verdict = WorkspacePathPolicy.CanRead(fullPath, authorizedRoot);
        if (!verdict.IsAllowed)
        {
            return WorkspaceReadResult.Refused(relative, verdict.Message);
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return WorkspaceReadResult.Refused(relative, "Ese archivo no existe.");
            }

            var size = WorkspacePathPolicy.CheckSize(info.Length);
            if (!size.IsAllowed)
            {
                return WorkspaceReadResult.Refused(relative, size.Message);
            }

            var raw = File.ReadAllText(fullPath);
            var redacted = WorkspaceSecretScanner.Redact(raw);

            return WorkspaceReadResult.Read(relative, redacted, secretsRedacted: redacted != raw);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return WorkspaceReadResult.Refused(relative, "No pude abrir ese archivo.");
        }
    }

    public IReadOnlyList<WorkspaceSearchHit> Search(string authorizedRoot, string query, int maximumHits)
    {
        if (string.IsNullOrWhiteSpace(query) || maximumHits <= 0)
        {
            return [];
        }

        var hits = new List<WorkspaceSearchHit>();
        var needle = query.Trim();

        foreach (var file in ListFiles(authorizedRoot, maximumFiles: 2000))
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file.FullPath);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // La línea que se devuelve se redacta igual que el contenido completo: una
                // coincidencia puede caer justo encima de un token.
                hits.Add(new WorkspaceSearchHit(
                    file.RelativePath,
                    index + 1,
                    WorkspaceSecretScanner.Redact(lines[index].Trim())));

                if (hits.Count >= maximumHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    private static IEnumerable<string> EnumerateSafely(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*", Enumeration);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string SafeRelativePath(string authorizedRoot, string fullPath)
    {
        try
        {
            return Path.GetRelativePath(authorizedRoot, fullPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return fullPath;
        }
    }
}
