using Nexo.Core.Permissions;

namespace Nexo.Core.Workspace;

/// <summary>
/// Diseño D12 (Fase 5) — la autorización del proyecto. Vacía por omisión: Kohana no tiene acceso a
/// ninguna carpeta hasta que la persona elige una, y revocar es dejarla vacía otra vez.
///
/// Una sola carpeta a propósito. Varias raíces multiplicarían la superficie autorizada sin que nadie
/// pudiera decir de memoria a qué tiene acceso Kohana, y "¿a qué tiene acceso?" tiene que poder
/// responderse en una frase.
/// </summary>
public sealed class WorkspaceSettings
{
    public string AuthorizedPath { get; set; } = string.Empty;

    public DateTimeOffset? AuthorizedAt { get; set; }

    public AutonomyLevel AutonomyLevel { get; set; } = AutonomyLevel.Guiar;

    public bool HasAuthorizedFolder => !string.IsNullOrWhiteSpace(AuthorizedPath);

    public void Normalize()
    {
        AuthorizedPath = (AuthorizedPath ?? string.Empty).Trim();

        if (!Enum.IsDefined(AutonomyLevel) || !WorkspaceAutonomyPolicy.IsAvailable(AutonomyLevel))
        {
            // Un archivo de ajustes escrito a mano no puede conceder un nivel que la política no
            // ofrece. Se cae al nivel por omisión en vez de aceptarlo: la autonomía no se hereda de
            // un archivo, se concede.
            AutonomyLevel = WorkspaceAutonomyPolicy.Default;
        }

        if (!HasAuthorizedFolder)
        {
            AuthorizedAt = null;
        }
    }

    public void Revoke()
    {
        AuthorizedPath = string.Empty;
        AuthorizedAt = null;
    }
}
