namespace Nexo.Core.Permissions;

/// <summary>Diseño D16 — permiso de una capacidad, con sus excepciones por aplicación.</summary>
public sealed class CapabilityPermission
{
    public KohanaCapability Capability { get; set; }

    public PermissionLevel Level { get; set; } = PermissionLevel.Preguntar;

    /// <summary>
    /// Aplicaciones excluidas de esta capacidad **aunque la capacidad esté permitida**. El modelo de
    /// confianza las nombra explícitamente: *"el usuario puede excluir aplicaciones, carpetas o tipos
    /// de dato específicos de cualquier capacidad, incluso si la capacidad en general está
    /// habilitada"*.
    /// </summary>
    public List<string> ExcludedApps { get; set; } = [];

    public CapabilityPermission Copy() => new()
    {
        Capability = Capability,
        Level = Level,
        ExcludedApps = [.. ExcludedApps]
    };
}

/// <summary>
/// Diseño D16 — el estado de permisos. Los valores por omisión son la parte que más importa:
///
/// - <see cref="KohanaCapability.ComputerUse"/> llega **Bloqueado**. Es el permiso más alto del
///   roadmap y no se concede por instalar Kohana.
/// - Todo lo demás llega **Preguntar**. No "Permitido": que una capacidad exista no significa que
///   pueda actuar sola.
///
/// Un archivo de ajustes escrito a mano tampoco puede saltarse esto: <see cref="Normalize"/>
/// reconstruye lo que falte con el valor conservador, nunca con el permisivo.
/// </summary>
public sealed class PermissionSettings
{
    public List<CapabilityPermission> Capabilities { get; set; } = [];

    public static PermissionLevel DefaultFor(KohanaCapability capability) =>
        capability == KohanaCapability.ComputerUse
            ? PermissionLevel.Bloqueado
            : PermissionLevel.Preguntar;

    public CapabilityPermission For(KohanaCapability capability)
    {
        var existing = Capabilities.FirstOrDefault(entry => entry.Capability == capability);
        if (existing is not null)
        {
            return existing;
        }

        var created = new CapabilityPermission
        {
            Capability = capability,
            Level = DefaultFor(capability)
        };

        Capabilities.Add(created);
        return created;
    }

    public void Normalize()
    {
        Capabilities ??= [];

        // Duplicados: gana el MÁS restrictivo. Un archivo con la misma capacidad dos veces no puede
        // resolverse "por la última que aparezca", porque entonces añadir una línea al final
        // ampliaría permisos sin que nadie lo confirmara.
        Capabilities = Capabilities
            .Where(entry => Enum.IsDefined(entry.Capability))
            .GroupBy(entry => entry.Capability)
            .Select(group =>
            {
                var winner = group.MinBy(entry => (int)entry.Level)!;
                winner.ExcludedApps = group
                    .SelectMany(entry => entry.ExcludedApps ?? [])
                    .Where(app => !string.IsNullOrWhiteSpace(app))
                    .Select(app => app.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!Enum.IsDefined(winner.Level))
                {
                    winner.Level = DefaultFor(winner.Capability);
                }

                return winner;
            })
            .ToList();

        foreach (var capability in Enum.GetValues<KohanaCapability>())
        {
            For(capability);
        }
    }

    public PermissionSettings Copy() => new()
    {
        Capabilities = Capabilities.Select(entry => entry.Copy()).ToList()
    };
}
