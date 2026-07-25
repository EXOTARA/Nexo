namespace Nexo.App.DailyFlow;

/// <summary>
/// Diseño D3 — coordinación mínima entre las cuatro vistas del flujo diario.
///
/// No contiene lógica de dominio: no sabe qué es una tarea, una sesión de enfoque o una rutina, y
/// no llama a ningún manager. Solo reenvía la señal "algo cambió" a quien esté escuchando.
///
/// Antes de D3, <c>MainWindow</c> ya escuchaba <c>TasksView.TasksChanged</c>,
/// <c>FocusView.FocusChanged</c> y <c>RoutinesView.ExecuteRequested</c>, y llamaba manualmente a
/// <c>RefreshHomeView()</c> en unos nueve sitios distintos — un patrón funcional pero disperso.
/// Este hub no reemplaza esos eventos por vista (siguen existiendo, MainWindow sigue
/// escuchándolos para su propia lógica de dominio); consolida el reenvío hacia Inicio en un solo
/// lugar, para que <see cref="Nexo.App.Views.HomeView"/> pueda suscribirse una vez y refrescarse
/// sola en vez de depender de que MainWindow se acuerde de llamarla en cada punto nuevo.
/// </summary>
public sealed class DailyFlowEventHub
{
    public event EventHandler? TasksChanged;

    public event EventHandler? FocusChanged;

    public event EventHandler? RoutinesChanged;

    public void RaiseTasksChanged() => TasksChanged?.Invoke(this, EventArgs.Empty);

    public void RaiseFocusChanged() => FocusChanged?.Invoke(this, EventArgs.Empty);

    public void RaiseRoutinesChanged() => RoutinesChanged?.Invoke(this, EventArgs.Empty);
}
