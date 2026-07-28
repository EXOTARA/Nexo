namespace Nexo.App.Ambient;

/// <summary>
/// Diseño D4.4 — instantánea de presentación de una entrada de historial ya archivada, calculada
/// por <see cref="AmbientRequestHistorySummaryBuilder"/>. Mismo rol que <c>FocusHistorySessionSummary</c>
/// tiene para el historial de Enfoque.
/// </summary>
public sealed record AmbientRequestHistoryItem(
    Guid Id,
    string Prompt,
    string StatusLabel,
    string TimestampText,
    string? ResultText,
    bool CanUndo,
    bool Undone);
