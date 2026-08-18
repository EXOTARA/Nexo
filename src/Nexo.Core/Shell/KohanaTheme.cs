namespace Nexo.Core.Shell;

/// <summary>Un tema con nombre, no un color suelto: es lo que se elige de la galería de Apariencia.</summary>
public sealed record KohanaTheme(string Id, string DisplayName, string Description, string AccentHex);

public static class KohanaThemeCatalog
{
    public static readonly IReadOnlyList<KohanaTheme> All =
    [
        new("sakura", "Sakura", "El look clásico de Kohana desde el primer día.", "#8B6CFF"),
        new("oceano", "Océano", "Frío y directo, para quien prefiere ir al grano.", "#4D8DFF"),
        new("bosque", "Bosque", "Tranquilo y verde, como el panel que te gustó.", "#35C58A"),
        new("cerezo", "Cerezo", "El tono floral original, más cálido.", "#F06CA8")
    ];

    /// <summary>
    /// Si el acento guardado coincide con uno de la galería, ese es el tema activo; si no, la
    /// persona lo eligió a mano en "Personalizado" y no hay tarjeta que resaltar por ella.
    /// </summary>
    public static KohanaTheme? FindByAccent(string accentHex) =>
        All.FirstOrDefault(theme =>
            string.Equals(theme.AccentHex, accentHex, StringComparison.OrdinalIgnoreCase));
}
