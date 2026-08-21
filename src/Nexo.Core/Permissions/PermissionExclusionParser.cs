namespace Nexo.Core.Permissions;

/// <summary>
/// Diseño D19 — traduce entre las exclusiones por aplicación y las líneas que la persona edita, en
/// formato <c>capacidad: aplicación</c>.
///
/// D16 dejó las exclusiones funcionando pero solo editables a mano en <c>settings.json</c>. Eran
/// justo la parte del modelo de confianza que más se usa a diario —*"el usuario puede excluir
/// aplicaciones… incluso si la capacidad en general está habilitada"*—, así que dejarlas sin
/// interfaz las volvía teóricas.
///
/// **Una línea mal escrita se ignora, pero se cuenta.** Mismo criterio que el diccionario de Flow en
/// D7: una línea que no hace nada y no avisa parece que la función está rota. Aquí importa más
/// todavía, porque una exclusión que la persona cree puesta y no lo está es una protección que no
/// existe.
/// </summary>
public static class PermissionExclusionParser
{
    public sealed record ParseOutcome(
        IReadOnlyList<(SakuraCapability Capability, string App)> Exclusions,
        int IgnoredLines);

    public static ParseOutcome Parse(IEnumerable<string>? lines)
    {
        var exclusions = new List<(SakuraCapability, string)>();
        var ignored = 0;

        foreach (var line in lines ?? [])
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                ignored++;
                continue;
            }

            var capabilityText = line[..separator].Trim();
            var app = line[(separator + 1)..].Trim();

            if (app.Length == 0 ||
                !Enum.TryParse<SakuraCapability>(capabilityText, ignoreCase: true, out var capability) ||
                !Enum.IsDefined(capability))
            {
                ignored++;
                continue;
            }

            exclusions.Add((capability, app));
        }

        return new ParseOutcome(exclusions, ignored);
    }

    /// <summary>
    /// Vuelca las exclusiones a líneas, para poder mostrarlas. Ordenadas por capacidad y luego por
    /// aplicación: un orden estable evita que la lista se reordene sola al guardar y parezca que
    /// cambió algo.
    /// </summary>
    public static IReadOnlyList<string> Format(PermissionSettings? settings) =>
        (settings?.Capabilities ?? [])
            .OrderBy(permission => permission.Capability.ToString(), StringComparer.Ordinal)
            .SelectMany(permission => (permission.ExcludedApps ?? [])
                .OrderBy(app => app, StringComparer.OrdinalIgnoreCase)
                .Select(app => $"{permission.Capability}: {app}"))
            .ToArray();

    /// <summary>
    /// Aplica las exclusiones parseadas, **reemplazando** las anteriores. Reemplazar y no acumular
    /// es lo que permite quitar una exclusión borrando su línea; si se acumularan, quitar sería
    /// imposible desde la interfaz.
    /// </summary>
    public static void Apply(PermissionSettings settings, IEnumerable<string>? lines)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var parsed = Parse(lines);

        foreach (var capability in Enum.GetValues<SakuraCapability>())
        {
            settings.For(capability).ExcludedApps = parsed.Exclusions
                .Where(exclusion => exclusion.Capability == capability)
                .Select(exclusion => exclusion.App)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
