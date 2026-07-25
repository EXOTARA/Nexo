using Nexo.App.DailyFlow;
using Nexo.Core.Automation;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D3 — <see cref="DailyFlowSummaryBuilder"/> es lógica pura extraída de
/// <c>MainWindow.RefreshHomeView()</c>: no necesita WPF ni <see cref="StaWpfFixture"/>, solo los
/// managers reales con fakes en memoria, igual que las pruebas de los propios managers en
/// <c>Nexo.Core.Tests</c>.
/// </summary>
public sealed class DailyFlowSummaryBuilderTests
{
    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 25, 10, 0, 0, TimeSpan.FromHours(-6));

    private static TaskManager CreateTaskManager(params NexoTask[] tasks)
    {
        var manager = new TaskManager(new FakeTaskStore(tasks));
        manager.Load();
        return manager;
    }

    private static FocusManager CreateFocusManager(FocusState? state = null)
    {
        var manager = new FocusManager(new FakeFocusStore(state));
        manager.Load();
        return manager;
    }

    // ---------- Tareas ----------

    [Fact]
    public void NoTasks_ShowsAnHonestEmptyState_NotAFabricatedValue()
    {
        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), CreateFocusManager(), [], false, ReferenceNow);

        Assert.Equal("0", model.TaskValue);
        Assert.Equal("Todavía no tienes tareas para hoy", model.TaskDetail);
    }

    [Fact]
    public void TasksToday_AreCountedAndTheNextOneIsNamed()
    {
        var manager = CreateTaskManager(
            new NexoTask { Title = "Enviar informe", DueAt = ReferenceNow.AddHours(2) },
            new NexoTask { Title = "Sin fecha" });

        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            manager, CreateFocusManager(), [], false, ReferenceNow);

        Assert.Equal("1", model.TaskValue);
        Assert.Contains("Enviar informe", model.TaskDetail);
    }

    [Fact]
    public void OverdueTasks_AreReportedWhenNothingIsDueToday()
    {
        var manager = CreateTaskManager(
            new NexoTask { Title = "Atrasada", DueAt = ReferenceNow.AddDays(-2) });

        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            manager, CreateFocusManager(), [], false, ReferenceNow);

        Assert.Equal("1", model.TaskValue);
        Assert.Contains("vencida", model.TaskDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompletedTasks_AreNotCountedAsPending()
    {
        var completed = new NexoTask { Title = "Ya lista", DueAt = ReferenceNow, CompletedAt = ReferenceNow };
        var manager = CreateTaskManager(completed);

        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            manager, CreateFocusManager(), [], false, ReferenceNow);

        Assert.Equal("0", model.TaskValue);
    }

    // ---------- Enfoque ----------

    [Fact]
    public void NoActiveSession_AndNothingFocusedToday_ShowsAnHonestEmptyState()
    {
        // Antes de D3 esta tarjeta mostraba "25 min" fijo incluso sin ninguna sesión — un valor
        // de relleno que se leía como una medición real.
        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), CreateFocusManager(), [], false, ReferenceNow);

        Assert.Equal("—", model.FocusValue);
        Assert.Equal("No hay una sesión de enfoque activa", model.FocusDetail);
    }

    [Fact]
    public void NoActiveSession_ButMinutesFocusedToday_ShowsTheRealAccumulatedTime()
    {
        var state = new FocusState
        {
            History =
            [
                new FocusHistoryEntry
                {
                    Label = "Enfoque",
                    Kind = FocusSessionKind.Focus,
                    StartedAt = ReferenceNow.AddMinutes(-25),
                    CompletedAt = ReferenceNow,
                    Duration = TimeSpan.FromMinutes(25)
                }
            ]
        };

        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), CreateFocusManager(state), [], false, ReferenceNow);

        Assert.Equal("25 min", model.FocusValue);
    }

    [Fact]
    public void ActiveSession_ShowsRemainingMinutesAndLabel()
    {
        var focusManager = CreateFocusManager();
        focusManager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);

        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), focusManager, [], false, ReferenceNow.AddMinutes(5));

        Assert.Equal("20 min", model.FocusValue);
        Assert.Equal("Sesión de enfoque", model.FocusDetail);
    }

    [Fact]
    public void PausedSession_SaysSoInTheDetail()
    {
        var focusManager = CreateFocusManager();
        focusManager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
        focusManager.Pause(ReferenceNow.AddMinutes(5));

        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), focusManager, [], false, ReferenceNow.AddMinutes(5));

        Assert.Contains("pausa", model.FocusDetail, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Rutinas ----------

    [Fact]
    public void NoRoutines_ShowsAnHonestEmptyState()
    {
        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), CreateFocusManager(), [], false, ReferenceNow);

        Assert.Equal("0", model.RoutineValue);
        Assert.Equal("No hay rutinas configuradas", model.RoutineDetail);
    }

    [Fact]
    public void RoutinesWithoutAnyExecution_CountsOnlyTheEnabledOnes()
    {
        RoutineDefinition[] routines =
        [
            new() { Name = "A", IsEnabled = true },
            new() { Name = "B", IsEnabled = false }
        ];

        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), CreateFocusManager(), routines, false, ReferenceNow);

        Assert.Equal("1", model.RoutineValue);
        Assert.DoesNotContain("Última", model.RoutineDetail);
    }

    [Fact]
    public void RoutinesWithAnExecution_ShowTheMostRecentRun()
    {
        RoutineDefinition[] routines =
        [
            new() { Name = "Antigua", IsEnabled = true, LastExecutedAt = ReferenceNow.AddDays(-3) },
            new() { Name = "Reciente", IsEnabled = true, LastExecutedAt = ReferenceNow.AddHours(-1) }
        ];

        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), CreateFocusManager(), routines, false, ReferenceNow);

        Assert.Contains("Reciente", model.RoutineDetail);
    }

    // ---------- Contexto visual ----------

    [Fact]
    public void NoRememberedWindow_ShowsTheDefaultInvitation()
    {
        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), CreateFocusManager(), [], false, ReferenceNow);

        Assert.Equal("Lista para analizar", model.ContextTitle);
    }

    [Fact]
    public void RememberedWindow_ShowsTheCaptureInvitation()
    {
        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), CreateFocusManager(), [], true, ReferenceNow);

        Assert.Equal("Ventana activa recordada", model.ContextTitle);
    }

    // ---------- Saludo ----------

    [Theory]
    [InlineData(7, "Buenos días")]
    [InlineData(14, "Buenas tardes")]
    [InlineData(22, "Buenas noches")]
    public void Greeting_MatchesTheHourOfDay(int hour, string expectedGreeting)
    {
        var now = new DateTimeOffset(2026, 7, 25, hour, 0, 0, TimeSpan.FromHours(-6));

        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            CreateTaskManager(), CreateFocusManager(), [], false, now);

        Assert.Equal(expectedGreeting, model.Greeting);
    }

    private sealed class FakeTaskStore(IEnumerable<NexoTask> tasks) : ITaskStore
    {
        private List<NexoTask> _tasks = tasks.Select(task => task.Copy()).ToList();

        public IReadOnlyList<NexoTask> Load() => _tasks.Select(task => task.Copy()).ToArray();

        public void Save(IReadOnlyCollection<NexoTask> tasks) => _tasks = tasks.Select(task => task.Copy()).ToList();
    }

    private sealed class FakeFocusStore(FocusState? state) : IFocusStore
    {
        private FocusState _state = state?.Copy() ?? new FocusState();

        public FocusState Load() => _state.Copy();

        public void Save(FocusState state) => _state = state.Copy();
    }
}
