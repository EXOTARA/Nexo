using Nexo.Core.Ambient;

namespace Nexo.App.Ambient;

/// <summary>
/// Diseño D4 — conecta <c>AmbientRequestManager</c> con el Sakura Pill Host, sin acumular esa
/// responsabilidad en <c>MainWindow</c>. Mismo rol que <c>FocusContinuityCoordinator</c> tiene para
/// Enfoque: recibe el manager y la ventana ya construidos, cablea las intenciones de la ventana
/// hacia el manager, y expone <see cref="Refresh"/> para que quien lo posea (MainWindow) lo llame
/// después de cualquier mutación relevante (igual que <c>CheckFocusTimer</c> llama a
/// <c>_focusContinuity.Refresh</c>).
/// </summary>
public sealed class SakuraPillCoordinator
{
    private readonly AmbientRequestManager _manager;
    private readonly SakuraPillWindow _pillWindow;

    public SakuraPillCoordinator(AmbientRequestManager manager, SakuraPillWindow pillWindow)
    {
        _manager = manager;
        _pillWindow = pillWindow;

        _pillWindow.CancelRequested += (_, _) =>
        {
            _manager.Cancel(DateTimeOffset.Now);
            Refresh();
        };

        _pillWindow.DismissRequested += (_, _) =>
        {
            _manager.Dismiss(DateTimeOffset.Now);
            Refresh();
        };

        _pillWindow.UndoRequested += (_, _) =>
        {
            var requestId = _manager.GetSnapshot(recentCount: 0).ActiveRequest?.Id;
            if (requestId.HasValue)
            {
                _manager.Undo(requestId.Value, DateTimeOffset.Now);
                Refresh();
            }
        };

        _pillWindow.QuickActionRequested += (_, actionId) =>
            QuickActionInvoked?.Invoke(this, actionId);
    }

    /// <summary>
    /// Se dispara cuando el usuario pulsa una acción rápida del resultado. Quien construya el
    /// coordinador decide qué hace cada acción (MainWindow conoce el catálogo disponible); el
    /// coordinador no interpreta el identificador.
    /// </summary>
    public event EventHandler<string>? QuickActionInvoked;

    public void Refresh()
    {
        var state = AmbientRequestDisplayStateBuilder.Build(_manager);
        _pillWindow.Apply(state);
    }
}
