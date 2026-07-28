using Nexo.Core.Ambient;

namespace Nexo.App.Ambient;

/// <summary>
/// Diseño D4.4 — traduce el historial real de <c>AmbientRequestManager</c> en una lista
/// presentable, más reciente primero. Lógica pura (sin WPF), mismo patrón que
/// <c>FocusHistorySummaryBuilder</c>: no inventa datos — una solicitud fallida muestra su mensaje
/// de error, no un resultado inexistente.
/// </summary>
public static class AmbientRequestHistorySummaryBuilder
{
    public const int MaxRecentEntries = 20;

    public static IReadOnlyList<AmbientRequestHistoryItem> Build(
        IReadOnlyList<AmbientRequestHistoryEntry> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        return history
            .OrderByDescending(entry => entry.CompletedAt)
            .Take(MaxRecentEntries)
            .Select(entry => new AmbientRequestHistoryItem(
                entry.Id,
                entry.Prompt,
                StatusLabel(entry.Status),
                FormatTimestamp(entry.CompletedAt),
                entry.Status == AmbientRequestStatus.Failed ? entry.ErrorMessage : entry.ResultShortText,
                entry.CanUndo,
                entry.Undone))
            .ToList();
    }

    private static string StatusLabel(AmbientRequestStatus status) => status switch
    {
        AmbientRequestStatus.Result => "Resuelta",
        AmbientRequestStatus.Cancelled => "Cancelada",
        AmbientRequestStatus.Failed => "Falló",
        _ => status.ToString()
    };

    private static string FormatTimestamp(DateTimeOffset completedAt) =>
        $"{completedAt.LocalDateTime:ddd d MMM · HH:mm}";
}
