using Nexo.Core.Vision;

namespace Nexo.Core.Memory;

/// <summary>
/// Diseño D9 (Fase 6) — la puerta de entrada de la memoria. El criterio de terminado de la fase es
/// literal: *"controles de exclusión y retención funcionando ANTES de que exista cualquier
/// almacenamiento de memoria de facto"*. Por eso esta política vive aparte del almacén y decide
/// sola si algo puede recordarse; el almacén no guarda nada que no haya pasado por aquí.
///
/// Cuatro razones para NO recordar, en orden:
/// 1. La memoria está apagada (por omisión lo está: el roadmap dice "nunca activada por defecto").
/// 2. Esa categoría concreta no está activada.
/// 3. El texto coincide con una exclusión del usuario.
/// 4. El texto contiene algo sensible evidente — se reutiliza el mismo
///    <see cref="SensitiveContentRedactor"/> de Lens en lugar de duplicar heurísticas: una
///    contraseña no debe acabar en la memoria aunque la persona la haya dictado sin querer.
/// </summary>
public static class MemoryPolicy
{
    public static MemoryOperationResult CanRemember(
        MemoryCategory category,
        string? text,
        MemorySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(text))
        {
            return MemoryOperationResult.Rejected("No hay nada que recordar.");
        }

        if (!settings.Enabled)
        {
            return MemoryOperationResult.Rejected(
                "La memoria está desactivada. Puedes activarla en Personalizar.");
        }

        if (!settings.IsCategoryEnabled(category))
        {
            return MemoryOperationResult.Rejected(
                $"No tienes activado que Kohana recuerde {CategoryLabel(category)}.");
        }

        var excluded = settings.MatchExclusion(text);
        if (excluded is not null)
        {
            return MemoryOperationResult.Rejected(
                $"No lo recordé porque coincide con tu exclusión «{excluded}».");
        }

        if (SensitiveContentRedactor.ContainsSensitiveContent(text))
        {
            return MemoryOperationResult.Rejected(
                "No lo recordé porque parece contener datos sensibles.");
        }

        return MemoryOperationResult.Completed("Se puede recordar.");
    }

    /// <summary>
    /// Diseño D9 — aplica la retención elegida. Una memoria sin caducidad es justo el riesgo que el
    /// roadmap señala para esta fase ("acumulación silenciosa de datos sensibles si la retención no
    /// tiene límites claros"), así que la retención siempre tiene un tope.
    /// </summary>
    public static IReadOnlyList<MemoryEntry> ApplyRetention(
        IEnumerable<MemoryEntry> entries,
        MemorySettings settings,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(settings);

        var cutoff = now.AddDays(-settings.RetentionDays);
        return entries
            .Where(entry => entry.CreatedAt >= cutoff)
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(MemorySettings.MaximumEntries)
            .ToArray();
    }

    public static string CategoryLabel(MemoryCategory category) => category switch
    {
        MemoryCategory.Preferencias => "tus preferencias",
        MemoryCategory.Conversacion => "el contexto de tus conversaciones",
        MemoryCategory.Habitos => "tus hábitos de uso",
        _ => category.ToString()
    };
}
