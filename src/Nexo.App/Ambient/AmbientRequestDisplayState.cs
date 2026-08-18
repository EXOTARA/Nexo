using Nexo.Core.Ambient;

namespace Nexo.App.Ambient;

/// <summary>
/// Diseño D4 — instantánea inmutable de "qué debe mostrarse y qué acciones tienen sentido ahora"
/// para el Sakura Pill Host, construida por <see cref="AmbientRequestDisplayStateBuilder"/> a
/// partir de <c>AmbientRequestManager</c>. Mismo rol que <c>FocusDisplayState</c> tiene para
/// Enfoque.
/// </summary>
public sealed record AmbientRequestDisplayState(
    bool IsVisible,
    Guid? RequestId,
    AmbientRequestStatus Status,
    string StatusText,
    string? ShortText,
    string? ExpandedText,
    string? ErrorMessage,
    IReadOnlyList<AmbientQuickAction> QuickActions,
    bool CanCancel,
    bool CanDismiss,
    bool CanUndo)
{
    public static readonly AmbientRequestDisplayState Hidden = new(
        IsVisible: false,
        RequestId: null,
        Status: AmbientRequestStatus.Cancelled,
        StatusText: string.Empty,
        ShortText: null,
        ExpandedText: null,
        ErrorMessage: null,
        QuickActions: [],
        CanCancel: false,
        CanDismiss: false,
        CanUndo: false);
}
