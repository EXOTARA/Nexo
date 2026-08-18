namespace Nexo.Core.Memory;

/// <summary>
/// Diseño D9 (Fase 6) — los controles que, según el criterio de terminado de la fase, tienen que
/// existir ANTES que el almacenamiento. Todo está apagado por omisión: el roadmap dice que la
/// memoria "nunca" se activa sola.
/// </summary>
public sealed class MemorySettings
{
    /// <summary>Tope duro de entradas, además de la retención por días.</summary>
    public const int MaximumEntries = 300;

    public const int DefaultRetentionDays = 30;
    public const int MinimumRetentionDays = 1;
    public const int MaximumRetentionDays = 365;

    /// <summary>Interruptor general. Apagado por omisión, a propósito.</summary>
    public bool Enabled { get; set; }

    public bool RememberPreferences { get; set; }

    public bool RememberConversation { get; set; }

    public bool RememberHabits { get; set; }

    public int RetentionDays { get; set; } = DefaultRetentionDays;

    /// <summary>Palabras o frases que nunca deben recordarse, aunque la memoria esté activa.</summary>
    public List<string> Exclusions { get; set; } = [];

    public bool IsCategoryEnabled(MemoryCategory category) => category switch
    {
        MemoryCategory.Preferencias => RememberPreferences,
        MemoryCategory.Conversacion => RememberConversation,
        MemoryCategory.Habitos => RememberHabits,
        _ => false
    };

    /// <summary>
    /// Devuelve la exclusión que coincide, o null. Compara sin distinguir mayúsculas y por
    /// contenido: una exclusión de "clínica" debe bloquear también "mi cita en la clínica".
    /// </summary>
    public string? MatchExclusion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || Exclusions is null)
        {
            return null;
        }

        return Exclusions.FirstOrDefault(exclusion =>
            !string.IsNullOrWhiteSpace(exclusion) &&
            text.Contains(exclusion.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void Normalize()
    {
        Exclusions ??= [];
        Exclusions = Exclusions
            .Where(exclusion => !string.IsNullOrWhiteSpace(exclusion))
            .Select(exclusion => exclusion.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        RetentionDays = Math.Clamp(RetentionDays, MinimumRetentionDays, MaximumRetentionDays);

        // Apagar el interruptor general apaga las categorías: si no, reactivar la memoria
        // resucitaría en silencio permisos que la persona creía revocados.
        if (!Enabled)
        {
            RememberPreferences = false;
            RememberConversation = false;
            RememberHabits = false;
        }
    }
}
