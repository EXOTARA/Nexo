using Nexo.Core.Ambient;

namespace Nexo.App.Ambient;

/// <summary>
/// Diseño D4 — función pura que transforma el estado real de <c>AmbientRequestManager</c> en un
/// <see cref="AmbientRequestDisplayState"/>. No conoce WPF; se prueba sin <c>StaWpfFixture</c>,
/// igual que <c>FocusDisplayStateBuilder</c>.
/// </summary>
public static class AmbientRequestDisplayStateBuilder
{
    public static AmbientRequestDisplayState Build(AmbientRequestManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var request = manager.GetSnapshot(recentCount: 0).ActiveRequest;
        if (request is null)
        {
            return AmbientRequestDisplayState.Hidden;
        }

        return request.Status switch
        {
            AmbientRequestStatus.Listening => new AmbientRequestDisplayState(
                IsVisible: true,
                RequestId: request.Id,
                Status: request.Status,
                StatusText: "Escuchando…",
                ShortText: request.Prompt,
                ExpandedText: null,
                ErrorMessage: null,
                QuickActions: [],
                CanCancel: true,
                CanDismiss: false,
                CanUndo: false),

            AmbientRequestStatus.Thinking => new AmbientRequestDisplayState(
                IsVisible: true,
                RequestId: request.Id,
                Status: request.Status,
                StatusText: "Pensando…",
                ShortText: request.Prompt,
                ExpandedText: null,
                ErrorMessage: null,
                QuickActions: [],
                CanCancel: true,
                CanDismiss: false,
                CanUndo: false),

            AmbientRequestStatus.Result => new AmbientRequestDisplayState(
                IsVisible: true,
                RequestId: request.Id,
                Status: request.Status,
                StatusText: "Listo",
                ShortText: request.Result?.ShortText,
                ExpandedText: request.Result?.ExpandedText,
                ErrorMessage: null,
                QuickActions: request.Result?.QuickActions ?? [],
                CanCancel: false,
                CanDismiss: true,
                CanUndo: request.Result?.CanUndo == true && !request.Undone),

            AmbientRequestStatus.Failed => new AmbientRequestDisplayState(
                IsVisible: true,
                RequestId: request.Id,
                Status: request.Status,
                StatusText: "No se pudo completar",
                ShortText: null,
                ExpandedText: null,
                ErrorMessage: request.ErrorMessage,
                QuickActions: [],
                CanCancel: false,
                CanDismiss: true,
                CanUndo: false),

            // AmbientRequestManager.Cancel() archiva la solicitud atómicamente (ver
            // ArchiveActiveLocked): una solicitud activa nunca queda observable en estado
            // Cancelada, así que este caso es inalcanzable en la práctica. Se deja explícito en
            // vez de agrupado bajo "_" para que un cambio futuro en el manager que sí deje una
            // solicitud activa cancelada visible falle aquí de forma ruidosa, no silenciosa.
            AmbientRequestStatus.Cancelled => throw new InvalidOperationException(
                "Una solicitud activa nunca debería observarse en estado Cancelada."),

            _ => throw new InvalidOperationException(
                $"Estado de solicitud ambiental no reconocido: {request.Status}")
        };
    }
}
