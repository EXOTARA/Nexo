using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Nexo.App.Views;
using Nexo.Core.Automation;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D3 — comportamiento real en WPF de las cuatro vistas del flujo diario, con managers
/// reales sobre almacenes en memoria (mismo patrón que <c>Nexo.Core.Tests</c>). Ninguna prueba
/// aquí pulsa un botón "Eliminar": esos ya muestran <see cref="MessageBox"/> real (confirmación),
/// que bloquearía la prueba esperando una respuesta que nunca llega. Esa lógica de confirmación
/// se prueba indirectamente comprobando que el manejador existe y que el manager subyacente
/// (<c>TaskManager.Delete</c>/<c>RoutineManager.Delete</c>) ya está cubierto en
/// <c>Nexo.Core.Tests</c>.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class DailyFlowWpfInteractionTests
{
    private readonly StaWpfFixture _fixture;

    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 25, 10, 0, 0, TimeSpan.FromHours(-6));

    public DailyFlowWpfInteractionTests(StaWpfFixture fixture) => _fixture = fixture;

    private static OffscreenHost CreateOffscreenHost(FrameworkElement content) => new(content);

    private sealed class OffscreenHost(FrameworkElement content) : IDisposable
    {
        public Window Window { get; } = new()
        {
            Width = 420,
            Height = 700,
            Left = -6000,
            Top = -6000,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Content = content
        };

        public void Show() => Window.Show();

        public void UpdateLayout() => Window.UpdateLayout();

        public void Dispose() => Window.Close();
    }

    // ---------- Hoy ----------

    [Fact]
    public void TasksView_OpenNewEditor_ShowsEditorAndFocusesTitle()
    {
        _fixture.Invoke(() =>
        {
            var manager = new TaskManager(new FakeTaskStore());
            manager.Load();
            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            view.OpenNewEditor();
            host.UpdateLayout();

            var editor = (FrameworkElement)view.FindName("EditorBorder")!;
            Assert.Equal(Visibility.Visible, editor.Visibility);
        });
    }

    [Fact]
    public void TasksView_ReopenButton_ReturnsACompletedTaskToPending()
    {
        _fixture.Invoke(() =>
        {
            var store = new FakeTaskStore();
            var manager = new TaskManager(store);
            manager.Load();
            var task = manager.Create("Tarea de prueba");
            manager.Complete(task.Id);

            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            InvokeFilterButton(view, "Completed");
            host.UpdateLayout();
            var reopenButton = FindButtonByAutomationName(view, "Reabrir tarea");
            Assert.NotNull(reopenButton);

            reopenButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            host.UpdateLayout();

            Assert.False(manager.GetAll().Single().IsCompleted);
        });
    }

    [Fact]
    public void TasksView_FocusButton_RaisesFocusRequestedWithTheTaskIdentity()
    {
        _fixture.Invoke(() =>
        {
            var manager = new TaskManager(new FakeTaskStore());
            manager.Load();
            var task = manager.Create("Escribir el informe");

            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            // La tarea no tiene vencimiento, así que el filtro "Hoy" (por defecto) no la muestra;
            // "Pendientes" sí, sin importar la fecha.
            InvokeFilterButton(view, "Pending");
            host.UpdateLayout();

            TaskFocusRequestedEventArgs? received = null;
            view.FocusRequested += (_, args) => received = args;

            var focusButton = FindButtonByAutomationName(view, "Enfocarme en esta tarea");
            Assert.NotNull(focusButton);
            focusButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.NotNull(received);
            Assert.Equal(task.Id, received!.TaskId);
            Assert.Equal("Escribir el informe", received.TaskTitle);
        });
    }

    [Fact]
    public void TasksView_IconButtons_AllHaveAccessibleNames()
    {
        _fixture.Invoke(() =>
        {
            var manager = new TaskManager(new FakeTaskStore());
            manager.Load();
            manager.Create("Tarea accesible");

            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            InvokeFilterButton(view, "Pending");
            host.UpdateLayout();

            foreach (var name in new[] { "Marcar como completada", "Editar tarea", "Eliminar tarea", "Enfocarme en esta tarea" })
            {
                Assert.NotNull(FindButtonByAutomationName(view, name));
            }
        });
    }

    // ---------- Enfoque ----------

    [Fact]
    public void FocusView_StartingAPreset_ShowsARunningSession()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var view = new FocusView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
            view.Refresh(ReferenceNow);
            host.UpdateLayout();

            var state = (TextBlock)view.FindName("TimerStateText")!;
            Assert.Equal("EN CURSO", state.Text);
        });
    }

    [Fact]
    public void FocusView_PrepareTaskAssociation_ThenStarting_ShowsTheAssociatedTaskBadge()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var taskId = Guid.NewGuid();
            var view = new FocusView(manager, id => id == taskId ? "Preparar la demo" : null);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            view.PrepareTaskAssociation(taskId, "Preparar la demo");
            var custom = (TextBox)view.FindName("CustomMinutesTextBox")!;
            custom.Text = "20";
            InvokeButtonByContent(view, "Iniciar");
            host.UpdateLayout();

            var badge = (FrameworkElement)view.FindName("AssociatedTaskBadge")!;
            var badgeText = (TextBlock)view.FindName("AssociatedTaskText")!;
            Assert.Equal(Visibility.Visible, badge.Visibility);
            Assert.Contains("Preparar la demo", badgeText.Text);
        });
    }

    [Fact]
    public void FocusView_FinishButton_RecordsHistory_DistinctFromCancel()
    {
        _fixture.Invoke(() =>
        {
            var store = new FakeFocusStore();
            var manager = new FocusManager(store);
            manager.Load();
            manager.Start(TimeSpan.FromMinutes(30), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);

            var view = new FocusView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            InvokeButtonByName(view, "FinishButton");
            host.UpdateLayout();

            Assert.Null(manager.GetSnapshot(ReferenceNow).ActiveTimer);
        });
    }

    [Fact]
    public void FocusView_CompletionPrompt_DismissHidesItWithoutCompletingAnything()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var view = new FocusView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var taskId = Guid.NewGuid();
            view.ShowTaskCompletionPrompt(taskId, "Tarea pendiente");
            host.UpdateLayout();

            var promptBorder = (FrameworkElement)view.FindName("TaskCompletionPromptBorder")!;
            Assert.Equal(Visibility.Visible, promptBorder.Visibility);

            var dismissButton = FindButtonByAutomationName(view, "Descartar sugerencia de completar tarea");
            Assert.NotNull(dismissButton);
            dismissButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            host.UpdateLayout();

            Assert.Equal(Visibility.Collapsed, promptBorder.Visibility);
        });
    }

    [Fact]
    public void FocusView_CompletionPrompt_CompleteButton_RaisesTheRequestWithoutActingOnItsOwn()
    {
        // La vista nunca completa la tarea por su cuenta: solo pide, quien construya la vista
        // decide qué hacer (aquí, MainWindow llama a TaskManager.Complete).
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var view = new FocusView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var taskId = Guid.NewGuid();
            view.ShowTaskCompletionPrompt(taskId, "Escribir el informe");
            host.UpdateLayout();

            TaskFocusRequestedEventArgs? received = null;
            view.CompleteAssociatedTaskRequested += (_, args) => received = args;

            var completeButton = FindButtonByAutomationName(view, "Marcar tarea asociada como completada");
            Assert.NotNull(completeButton);
            completeButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.NotNull(received);
            Assert.Equal(taskId, received!.TaskId);
        });
    }

    // ---------- Rutinas ----------

    [Fact]
    public void RoutinesView_ToggleButton_FlipsEnabledState()
    {
        _fixture.Invoke(() =>
        {
            var manager = new RoutineManager(new FakeRoutineStore());
            manager.Load();
            var routine = manager.GetAll().First();
            Assert.True(routine.IsEnabled);

            var view = new RoutinesView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var toggleName = $"Desactivar {routine.Name}";
            var toggleButton = FindButtonByAutomationName(view, toggleName);
            Assert.NotNull(toggleButton);
            toggleButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            host.UpdateLayout();

            Assert.False(manager.GetAll().Single(candidate => candidate.Id == routine.Id).IsEnabled);
        });
    }

    [Fact]
    public void RoutinesView_ShowsNeverExecutedUntilItRuns()
    {
        _fixture.Invoke(() =>
        {
            var manager = new RoutineManager(new FakeRoutineStore());
            manager.Load();
            var view = new RoutinesView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var list = (ItemsControl)view.FindName("RoutinesItemsControl")!;
            var texts = list.Items.Cast<object>()
                .Select(item => (string)item.GetType().GetProperty("LastExecutionText")!.GetValue(item)!)
                .ToArray();

            Assert.All(texts, text => Assert.Equal("Nunca ejecutada", text));
        });
    }

    [Fact]
    public void RoutinesView_AfterRecordingAnExecution_ShowsTheLastRun()
    {
        _fixture.Invoke(() =>
        {
            var manager = new RoutineManager(new FakeRoutineStore());
            manager.Load();
            var routine = manager.GetAll().First();
            manager.RecordExecution(routine.Id, ReferenceNow, succeeded: true);

            var view = new RoutinesView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var list = (ItemsControl)view.FindName("RoutinesItemsControl")!;
            var item = list.Items.Cast<object>()
                .First(candidate => (Guid)candidate.GetType().GetProperty("Id")!.GetValue(candidate)! == routine.Id);
            var text = (string)item.GetType().GetProperty("LastExecutionText")!.GetValue(item)!;

            Assert.Contains("Última vez", text);
        });
    }

    // ---------- Inicio ----------

    [Fact]
    public void HomeView_Refresh_ShowsRoutineCardValues()
    {
        _fixture.Invoke(() =>
        {
            var view = new HomeView();
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            view.Refresh(new HomeDashboardViewModel(
                "Buenos días", "hoy", "0", "Todavía no tienes tareas para hoy",
                "—", "No hay una sesión de enfoque activa", false, false,
                "2", "2 disponibles",
                "Lista para analizar", "detalle"));
            host.UpdateLayout();

            var routineCount = (TextBlock)view.FindName("RoutineCountText")!;
            Assert.Equal("2", routineCount.Text);
        });
    }

    [Fact]
    public void HomeView_QuickActionButtons_ExistAndAreWired()
    {
        // Confirma que cada manejador de clic existe en el code-behind y que el evento
        // correspondiente está declarado (evita que un handler quede huérfano si el botón de XAML
        // se renombra sin actualizar el .cs).
        (string HandlerMethodName, string EventName)[] pairs =
        [
            ("NewTaskQuickAction_Click", nameof(HomeView.NewTaskRequested)),
            ("StartFocusQuickAction_Click", nameof(HomeView.StartFocusRequested)),
            ("CommandCenterQuickAction_Click", nameof(HomeView.CommandCenterRequested))
        ];

        foreach (var (handlerMethodName, eventName) in pairs)
        {
            Assert.NotNull(typeof(HomeView).GetMethod(
                handlerMethodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            Assert.NotNull(typeof(HomeView).GetEvent(eventName));
        }
    }

    [Fact]
    public void HomeView_RoutinesCard_RaisesRoutinesRequested()
    {
        _fixture.Invoke(() =>
        {
            var view = new HomeView();
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var raised = false;
            view.RoutinesRequested += (_, _) => raised = true;

            var routinesCard = FindButtonByAutomationName(view, "Rutinas. Ir a Rutinas");
            Assert.NotNull(routinesCard);
            routinesCard!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.True(raised);
        });
    }

    // ---------- Ayudantes ----------

    private static void InvokeFilterButton(TasksView view, string tag)
    {
        var button = FindDescendants<Button>(view).First(candidate => (candidate.Tag as string) == tag);
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    private static void InvokeButtonByName(FrameworkElement root, string name)
    {
        var button = (Button)root.FindName(name)!;
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    private static void InvokeButtonByContent(DependencyObject root, string content)
    {
        var button = FindDescendants<Button>(root).First(candidate => (candidate.Content as string) == content);
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    private static Button? FindButtonByAutomationName(DependencyObject root, string automationName) =>
        FindDescendants<Button>(root).FirstOrDefault(button =>
            AutomationProperties.GetName(button) == automationName);

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class FakeTaskStore : ITaskStore
    {
        private List<NexoTask> _tasks = [];

        public IReadOnlyList<NexoTask> Load() => _tasks.Select(task => task.Copy()).ToArray();

        public void Save(IReadOnlyCollection<NexoTask> tasks) => _tasks = tasks.Select(task => task.Copy()).ToList();
    }

    private sealed class FakeFocusStore : IFocusStore
    {
        private FocusState _state = new();

        public FocusState Load() => _state.Copy();

        public void Save(FocusState state) => _state = state.Copy();
    }

    private sealed class FakeRoutineStore : IRoutineStore
    {
        private RoutineState? _state;

        public RoutineState Load() => _state?.Copy() ?? new RoutineState();

        public void Save(RoutineState state) => _state = state.Copy();
    }
}
