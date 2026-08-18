namespace Nexo.Core.Workspace;

/// <summary>Diseño D12 — un archivo del proyecto, tal y como Kohana lo ve.</summary>
public sealed record WorkspaceFile(string FullPath, string RelativePath, long SizeInBytes);

/// <summary>Diseño D12 — el contenido ya filtrado, o el motivo por el que no se pudo leer.</summary>
public sealed record WorkspaceReadResult(
    bool Success,
    string RelativePath,
    string Content,
    string Message,
    bool SecretsRedacted)
{
    public static WorkspaceReadResult Read(string relativePath, string content, bool secretsRedacted) =>
        new(true, relativePath, content, "Leído.", secretsRedacted);

    public static WorkspaceReadResult Refused(string relativePath, string message) =>
        new(false, relativePath, string.Empty, message, false);
}

/// <summary>
/// Diseño D12 (Fase 5) — acceso de SOLO LECTURA al proyecto autorizado. La interfaz no tiene ningún
/// método de escritura, y eso es el diseño, no un pendiente: mientras la capacidad esté en los
/// niveles 1–3 del modelo de confianza, no debe existir siquiera la forma de llamar a una escritura.
/// Cuando llegue el nivel 4 hará falta antes el snapshot por archivo y el Audit Log.
/// </summary>
public interface IWorkspaceReader
{
    /// <summary>Archivos legibles del proyecto, ya filtrados por <see cref="WorkspacePathPolicy"/>.</summary>
    IReadOnlyList<WorkspaceFile> ListFiles(string authorizedRoot, int maximumFiles);

    WorkspaceReadResult ReadFile(string authorizedRoot, string fullPath);

    /// <summary>Búsqueda literal en el contenido. Devuelve rutas y la línea que coincide.</summary>
    IReadOnlyList<WorkspaceSearchHit> Search(string authorizedRoot, string query, int maximumHits);
}

public sealed record WorkspaceSearchHit(string RelativePath, int LineNumber, string Line);
