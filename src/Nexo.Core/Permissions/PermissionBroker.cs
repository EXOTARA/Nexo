namespace Nexo.Core.Permissions;

/// <summary>Diseño D16 — qué hacer con una petición: hacerla, preguntar, o negarse.</summary>
public enum PermissionOutcome
{
    Denegado,

    /// <summary>Hace falta una confirmación explícita antes de actuar.</summary>
    RequiereConfirmacion,

    Permitido
}

/// <summary>
/// Diseño D16 — lo que quiere hacer una capacidad. La petición declara sus categorías obligatorias:
/// quien pide es quien sabe si lo que va a hacer borra algo, envía algo fuera o toca credenciales.
/// Declararlo de menos no es una omisión inofensiva, así que la lista viaja con la petición y queda
/// en el registro.
/// </summary>
public sealed record PermissionRequest(
    KohanaCapability Capability,
    string Description,
    string? TargetApp = null,
    IReadOnlyList<MandatoryConfirmation>? Categories = null)
{
    public IReadOnlyList<MandatoryConfirmation> MandatoryCategories => Categories ?? [];
}

public sealed record PermissionDecision(
    PermissionOutcome Outcome,
    string Reason,
    IReadOnlyList<MandatoryConfirmation> TriggeredCategories)
{
    public bool MayProceedWithoutAsking => Outcome == PermissionOutcome.Permitido;

    public bool IsDenied => Outcome == PermissionOutcome.Denegado;
}

/// <summary>
/// Diseño D16 (prerrequisito de la Fase 7) — el Permission Broker. El roadmap lo nombra como
/// condición para habilitar Computer Use: *"requiere el Permission Broker y el Audit Log completos
/// antes de habilitarse"*. El Audit Log llegó en D13; esto es la otra mitad.
///
/// Una sola función pura decide, y el orden de las comprobaciones ES la política:
///
/// 1. **Exclusión por aplicación** → denegado. Está primero a propósito: el modelo de confianza dice
///    que una exclusión vale *"incluso si la capacidad en general está habilitada"*, así que tiene
///    que ganarle a todo, incluido a Permitido.
/// 2. **Capacidad bloqueada** → denegado.
/// 3. **Categoría de confirmación obligatoria** → confirmación, sea cual sea el nivel. Son las siete
///    que el modelo marca como obligatorias *"sin importar el nivel de autonomía configurado"*.
///    Estar en Permitido no las salta; ése es justamente el punto.
/// 4. **Permitido** → adelante.
/// 5. **Cualquier otra cosa** → confirmación. Falla cerrado: un nivel que no se reconozca no se
///    interpreta como permiso.
/// </summary>
public static class PermissionBroker
{
    public static PermissionDecision Decide(PermissionRequest request, PermissionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(settings);

        var permission = settings.For(request.Capability);
        var categories = request.MandatoryCategories
            .Where(Enum.IsDefined)
            .Distinct()
            .ToArray();

        var excluded = MatchExcludedApp(permission, request.TargetApp);
        if (excluded is not null)
        {
            return new PermissionDecision(
                PermissionOutcome.Denegado,
                $"Excluiste «{excluded}» de esta capacidad, así que no la toco.",
                categories);
        }

        if (permission.Level == PermissionLevel.Bloqueado)
        {
            return new PermissionDecision(
                PermissionOutcome.Denegado,
                $"{CapabilityText.Describe(request.Capability)} está bloqueada. Puedes cambiarlo en Personalizar.",
                categories);
        }

        if (categories.Length > 0)
        {
            var reasons = string.Join(", ", categories.Select(MandatoryConfirmationText.Describe));
            return new PermissionDecision(
                PermissionOutcome.RequiereConfirmacion,
                $"Esto siempre te lo pregunto porque {reasons}.",
                categories);
        }

        if (permission.Level == PermissionLevel.Permitido)
        {
            return new PermissionDecision(
                PermissionOutcome.Permitido,
                $"Tienes {CapabilityText.Describe(request.Capability)} permitida.",
                categories);
        }

        return new PermissionDecision(
            PermissionOutcome.RequiereConfirmacion,
            $"Tienes {CapabilityText.Describe(request.Capability)} en «preguntar».",
            categories);
    }

    /// <summary>
    /// Diseño D16 — política de mínimo privilegio del modelo de confianza: *"ampliar el alcance de
    /// una capacidad ya habilitada requiere una nueva confirmación explícita, no se hereda del
    /// permiso original"*. Subir de nivel es ampliar; bajarlo, no.
    /// </summary>
    public static bool RequiresNewConfirmation(PermissionLevel current, PermissionLevel desired) =>
        desired > current;

    private static string? MatchExcludedApp(CapabilityPermission permission, string? targetApp)
    {
        if (string.IsNullOrWhiteSpace(targetApp) || permission.ExcludedApps is null)
        {
            return null;
        }

        // Comparación por contenido y sin distinguir mayúsculas, igual que las exclusiones de la
        // memoria: excluir "banco" tiene que tapar también "Banco - Google Chrome".
        return permission.ExcludedApps.FirstOrDefault(app =>
            !string.IsNullOrWhiteSpace(app) &&
            targetApp.Contains(app.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public static class CapabilityText
{
    public static string Describe(KohanaCapability capability) => capability switch
    {
        KohanaCapability.Lens => "ver la pantalla",
        KohanaCapability.Flow => "el dictado global",
        KohanaCapability.Memoria => "la memoria personal",
        KohanaCapability.Proyecto => "el acceso a tu proyecto",
        KohanaCapability.Optimizacion => "optimizar el equipo",
        KohanaCapability.ComputerUse => "actuar sobre tu equipo",
        _ => capability.ToString()
    };
}
