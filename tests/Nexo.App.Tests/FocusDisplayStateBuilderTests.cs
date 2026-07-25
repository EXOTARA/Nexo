using Nexo.App.DailyFlow;
using Nexo.Core.Focus;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D3.1 — <see cref="FocusDisplayStateBuilder"/> es lógica pura (sin WPF), la misma fuente
/// que ya alimenta FocusView, el mini temporizador global y la tarjeta de Enfoque en Inicio. No
/// necesita <see cref="StaWpfFixture"/>, igual que <c>DailyFlowSummaryBuilderTests</c>.
/// </summary>
public sealed class FocusDisplayStateBuilderTests
{
    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 25, 10, 0, 0, TimeSpan.FromHours(-6));

    private static FocusManager CreateManager(FocusState? state = null)
    {
        var manager = new FocusManager(new FakeFocusStore(state));
        manager.Load();
        return manager;
    }

    [Fact]
    public void NoSession_ReturnsAnHonestEmptyState()
    {
        var state = FocusDisplayStateBuilder.Build(CreateManager(), ReferenceNow);

        Assert.False(state.HasSession);
        Assert.False(state.IsVisible);
        Assert.False(state.IsPaused);
        Assert.Equal("00:00", state.ClockText);
        Assert.Null(state.AssociatedLabel);
        Assert.True(state.CanStart);
        Assert.False(state.CanPause);
        Assert.False(state.CanResume);
        Assert.False(state.CanFinish);
    }

    [Fact]
    public void ActiveSession_IsVisibleWithTheRealRemainingClock()
    {
        var manager = CreateManager();
        manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);

        var state = FocusDisplayStateBuilder.Build(manager, ReferenceNow.AddMinutes(5));

        Assert.True(state.HasSession);
        Assert.True(state.IsVisible);
        Assert.False(state.IsPaused);
        Assert.Equal("20:00", state.ClockText);
        Assert.True(state.CanPause);
        Assert.False(state.CanResume);
        Assert.True(state.CanFinish);
        Assert.False(state.CanStart);
    }

    [Fact]
    public void PausedSession_KeepsAStableRemainingTimeAndFlipsTheFlags()
    {
        var manager = CreateManager();
        manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
        manager.Pause(ReferenceNow.AddMinutes(5));

        var stateRightAfterPause = FocusDisplayStateBuilder.Build(manager, ReferenceNow.AddMinutes(5));
        var stateMuchLater = FocusDisplayStateBuilder.Build(manager, ReferenceNow.AddMinutes(45));

        Assert.True(stateRightAfterPause.IsPaused);
        Assert.Equal("20:00", stateRightAfterPause.ClockText);
        // Diseño D3.1: pausado no debe decrementar solo por dejar pasar tiempo real.
        Assert.Equal(stateRightAfterPause.ClockText, stateMuchLater.ClockText);
        Assert.True(stateRightAfterPause.CanResume);
        Assert.False(stateRightAfterPause.CanPause);
    }

    [Fact]
    public void ActiveSession_WithAnAssociatedTask_ResolvesTheRealTaskTitle()
    {
        var manager = CreateManager();
        var taskId = Guid.NewGuid();
        manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow, taskId);

        var state = FocusDisplayStateBuilder.Build(
            manager, ReferenceNow, id => id == taskId ? "Escribir el informe" : null);

        Assert.Equal("Escribir el informe", state.AssociatedLabel);
    }

    [Fact]
    public void ActiveSession_WithADeletedTask_FallsBackToTheGenericSessionLabel()
    {
        var manager = CreateManager();
        var taskId = Guid.NewGuid();
        manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow, taskId);

        // El resolutor no encuentra la tarea (fue borrada): no debe quedar un rótulo vacío ni
        // lanzar, sino caer de vuelta a la etiqueta de la sesión.
        var state = FocusDisplayStateBuilder.Build(manager, ReferenceNow, _ => null);

        Assert.Equal("Sesión de enfoque", state.AssociatedLabel);
    }

    [Fact]
    public void ActiveSession_WithoutAResolver_UsesTheGenericSessionLabel()
    {
        var manager = CreateManager();
        manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);

        var state = FocusDisplayStateBuilder.Build(manager, ReferenceNow);

        Assert.Equal("Sesión de enfoque", state.AssociatedLabel);
    }

    [Fact]
    public void NoSession_WithMinutesFocusedToday_ReflectsTheRealSummary_NotAFabricatedValue()
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
        var manager = CreateManager(state);

        var displayState = FocusDisplayStateBuilder.Build(manager, ReferenceNow);

        Assert.False(displayState.HasSession);
        Assert.Equal(25, displayState.FocusMinutesToday);
        Assert.Equal(1, displayState.CompletedSessionsToday);
    }

    [Fact]
    public void ActiveSession_ProgressPercent_ReflectsTheElapsedFraction()
    {
        var manager = CreateManager();
        manager.Start(TimeSpan.FromMinutes(20), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);

        var state = FocusDisplayStateBuilder.Build(manager, ReferenceNow.AddMinutes(5));

        Assert.Equal(25, state.ProgressPercent, 0);
    }

    private sealed class FakeFocusStore(FocusState? state = null) : IFocusStore
    {
        private FocusState _state = state?.Copy() ?? new FocusState();

        public FocusState Load() => _state.Copy();

        public void Save(FocusState state) => _state = state.Copy();
    }
}
