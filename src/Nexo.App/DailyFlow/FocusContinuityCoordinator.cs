using Nexo.App.Views;
using Nexo.Core.Focus;

namespace Nexo.App.DailyFlow;

/// <summary>
/// Diseño D3.1 — conecta <c>FocusManager</c>, el mini temporizador global y la navegación a
/// Enfoque, sin acumular esa responsabilidad en <c>MainWindow</c>. Recibe un puñado de delegados
/// pequeños en el constructor (navegar, resolver el título de una tarea) en vez de una referencia
/// a <c>MainWindow</c> completa, para no acoplarse a la ventana.
///
/// No crea ningún <see cref="System.Windows.Threading.DispatcherTimer"/> propio: quien lo
/// construya (MainWindow) sigue llamando a <see cref="Refresh"/> desde el único tick de Enfoque ya
/// existente.
/// </summary>
public sealed class FocusContinuityCoordinator
{
    private readonly FocusManager _focusManager;
    private readonly FocusMiniTimer _miniTimer;
    private readonly Func<Guid, string?> _resolveTaskTitle;
    private readonly Action _openFocus;

    public FocusContinuityCoordinator(
        FocusManager focusManager,
        FocusMiniTimer miniTimer,
        Func<Guid, string?> resolveTaskTitle,
        Action openFocus)
    {
        _focusManager = focusManager;
        _miniTimer = miniTimer;
        _resolveTaskTitle = resolveTaskTitle;
        _openFocus = openFocus;

        _miniTimer.OpenRequested += (_, _) => _openFocus();
        _miniTimer.PauseRequested += (_, _) => _focusManager.Pause(DateTimeOffset.Now);
        _miniTimer.ResumeRequested += (_, _) => _focusManager.Resume(DateTimeOffset.Now);
        _miniTimer.FinishRequested += (_, _) => FinishRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Se dispara cuando el usuario pide finalizar desde el mini temporizador. Quien construya el
    /// coordinador decide cómo finalizar (MainWindow ya tiene la lógica de "capturar el TaskId
    /// antes de Finish() y ofrecer completar la tarea" — no se duplica aquí).
    /// </summary>
    public event EventHandler? FinishRequested;

    /// <summary>
    /// Recalcula el estado de presentación y lo aplica al mini temporizador. Se llama desde el
    /// mismo punto central que ya refresca Enfoque e Inicio (<c>CheckFocusTimer</c> en
    /// <c>MainWindow</c>), no desde un timer propio.
    /// </summary>
    public void Refresh(DateTimeOffset now)
    {
        var state = FocusDisplayStateBuilder.Build(_focusManager, now, _resolveTaskTitle);
        _miniTimer.Apply(state);
    }
}
