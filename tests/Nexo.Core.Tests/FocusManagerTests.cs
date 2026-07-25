using Nexo.Core.Focus;

namespace Nexo.Core.Tests;

public sealed class FocusManagerTests
{
    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 19, 10, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void Start_PersistsActiveTimer()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();

        var result = manager.Start(
            TimeSpan.FromMinutes(25),
            "Sesión de enfoque",
            FocusSessionKind.Focus,
            ReferenceNow);

        Assert.True(result.Success);
        Assert.NotNull(store.State.ActiveTimer);
        Assert.Equal(ReferenceNow.AddMinutes(25), store.State.ActiveTimer?.EndsAt);
    }

    [Fact]
    public void Start_RejectsSecondActiveTimer()
    {
        var manager = CreateManager();
        manager.Start(
            TimeSpan.FromMinutes(25),
            "Enfoque",
            FocusSessionKind.Focus,
            ReferenceNow);

        var result = manager.Start(
            TimeSpan.FromMinutes(5),
            "Descanso",
            FocusSessionKind.Break,
            ReferenceNow.AddMinutes(1));

        Assert.False(result.Success);
    }

    [Fact]
    public void PauseAndResume_PreserveRemainingTime()
    {
        var manager = CreateManager();
        manager.Start(
            TimeSpan.FromMinutes(30),
            "Estudio",
            FocusSessionKind.Study,
            ReferenceNow);

        manager.Pause(ReferenceNow.AddMinutes(10));
        var paused = manager.GetSnapshot(ReferenceNow.AddMinutes(20));
        manager.Resume(ReferenceNow.AddMinutes(20));
        var resumed = manager.GetSnapshot(ReferenceNow.AddMinutes(25));

        Assert.Equal(TimeSpan.FromMinutes(20), paused.Remaining);
        Assert.Equal(TimeSpan.FromMinutes(15), resumed.Remaining);
    }

    [Fact]
    public void CollectCompletion_DeliversOnlyOnceAndAddsHistory()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        manager.Start(
            TimeSpan.FromMinutes(1),
            "Enfoque",
            FocusSessionKind.Focus,
            ReferenceNow);

        var first = manager.CollectCompletion(ReferenceNow.AddMinutes(1));
        var second = manager.CollectCompletion(ReferenceNow.AddMinutes(2));

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Null(store.State.ActiveTimer);
        Assert.Single(store.State.History);
    }

    [Fact]
    public void GetSnapshot_CountsFocusMinutesButNotBreakMinutes()
    {
        var store = new MemoryFocusStore(new FocusState
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
                },
                new FocusHistoryEntry
                {
                    Label = "Descanso",
                    Kind = FocusSessionKind.Break,
                    StartedAt = ReferenceNow.AddMinutes(-5),
                    CompletedAt = ReferenceNow,
                    Duration = TimeSpan.FromMinutes(5)
                }
            ]
        });
        var manager = new FocusManager(store);
        manager.Load();

        var snapshot = manager.GetSnapshot(ReferenceNow);

        Assert.Equal(2, snapshot.CompletedSessionsToday);
        Assert.Equal(25, snapshot.FocusMinutesToday);
    }

    [Fact]
    public void Load_RestoresRunningTimer()
    {
        var store = new MemoryFocusStore(new FocusState
        {
            ActiveTimer = new FocusTimer
            {
                Label = "Trabajo",
                Duration = TimeSpan.FromMinutes(40),
                StartedAt = ReferenceNow,
                EndsAt = ReferenceNow.AddMinutes(40),
                PausedRemaining = TimeSpan.FromMinutes(40),
                Status = FocusTimerStatus.Running
            }
        });
        var manager = new FocusManager(store);

        manager.Load();
        var snapshot = manager.GetSnapshot(ReferenceNow.AddMinutes(10));

        Assert.NotNull(snapshot.ActiveTimer);
        Assert.Equal(TimeSpan.FromMinutes(30), snapshot.Remaining);
    }

    // ---------- Diseño D3: Finish ----------

    [Fact]
    public void Finish_RecordsElapsedTime_NotTheFullPlannedDuration()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        manager.Start(TimeSpan.FromMinutes(30), "Enfoque", FocusSessionKind.Focus, ReferenceNow);

        var result = manager.Finish(ReferenceNow.AddMinutes(10));

        Assert.True(result.Success);
        Assert.Null(store.State.ActiveTimer);
        var entry = Assert.Single(store.State.History);
        Assert.Equal(TimeSpan.FromMinutes(10), entry.Duration);
    }

    [Fact]
    public void Cancel_NeverAddsHistory_UnlikeFinish()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        manager.Start(TimeSpan.FromMinutes(30), "Enfoque", FocusSessionKind.Focus, ReferenceNow);

        manager.Cancel();

        Assert.Empty(store.State.History);
    }

    [Fact]
    public void Finish_WhilePaused_UsesThePausedRemainingTime()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        manager.Start(TimeSpan.FromMinutes(30), "Enfoque", FocusSessionKind.Focus, ReferenceNow);
        manager.Pause(ReferenceNow.AddMinutes(12));

        var result = manager.Finish(ReferenceNow.AddMinutes(20));

        Assert.True(result.Success);
        var entry = Assert.Single(store.State.History);
        Assert.Equal(TimeSpan.FromMinutes(12), entry.Duration);
    }

    [Fact]
    public void Finish_WithNoElapsedTime_BehavesLikeCancel_NoEmptyHistoryEntry()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        manager.Start(TimeSpan.FromMinutes(30), "Enfoque", FocusSessionKind.Focus, ReferenceNow);

        var result = manager.Finish(ReferenceNow);

        Assert.True(result.Success);
        Assert.Null(store.State.ActiveTimer);
        Assert.Empty(store.State.History);
    }

    [Fact]
    public void Finish_WithoutAnActiveTimer_Fails()
    {
        var manager = CreateManager();

        var result = manager.Finish(ReferenceNow);

        Assert.False(result.Success);
    }

    [Fact]
    public void Finish_PreservesTheAssociatedTaskInHistory()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        var taskId = Guid.NewGuid();
        manager.Start(TimeSpan.FromMinutes(30), "Enfoque", FocusSessionKind.Focus, ReferenceNow, taskId);

        manager.Finish(ReferenceNow.AddMinutes(5));

        Assert.Equal(taskId, store.State.History.Single().TaskId);
    }

    // ---------- Diseño D3: asociación con una tarea ----------

    [Fact]
    public void Start_WithoutATaskId_LeavesTaskIdNull()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();

        manager.Start(TimeSpan.FromMinutes(25), "Enfoque", FocusSessionKind.Focus, ReferenceNow);

        Assert.Null(store.State.ActiveTimer?.TaskId);
    }

    [Fact]
    public void Start_WithATaskId_PersistsIt()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        var taskId = Guid.NewGuid();

        manager.Start(TimeSpan.FromMinutes(25), "Enfoque", FocusSessionKind.Focus, ReferenceNow, taskId);

        Assert.Equal(taskId, store.State.ActiveTimer?.TaskId);
    }

    [Fact]
    public void CollectCompletion_PreservesTheAssociatedTaskInHistory()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        var taskId = Guid.NewGuid();
        manager.Start(TimeSpan.FromMinutes(1), "Enfoque", FocusSessionKind.Focus, ReferenceNow, taskId);

        var completion = manager.CollectCompletion(ReferenceNow.AddMinutes(1));

        Assert.Equal(taskId, completion?.TaskId);
        Assert.Equal(taskId, store.State.History.Single().TaskId);
    }

    // ---------- Diseño D3.1: historial expuesto para el resumen de actividad reciente ----------

    [Fact]
    public void GetHistory_OnFreshManager_ReturnsEmpty()
    {
        var manager = CreateManager();

        Assert.Empty(manager.GetHistory());
    }

    [Fact]
    public void GetHistory_ReturnsRecordedSessionsWithRealDuration()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        manager.Start(TimeSpan.FromMinutes(30), "Enfoque", FocusSessionKind.Focus, ReferenceNow);
        manager.Finish(ReferenceNow.AddMinutes(12));

        var history = manager.GetHistory();

        var entry = Assert.Single(history);
        Assert.Equal("Enfoque", entry.Label);
        Assert.Equal(TimeSpan.FromMinutes(12), entry.Duration);
    }

    [Fact]
    public void GetHistory_DoesNotExposeTheInternalListInstance()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        manager.Start(TimeSpan.FromMinutes(10), "Enfoque", FocusSessionKind.Focus, ReferenceNow);
        manager.Finish(ReferenceNow.AddMinutes(5));

        var first = manager.GetHistory();
        var second = manager.GetHistory();

        Assert.NotSame(first, second);
        Assert.Equal(first.Single().Id, second.Single().Id);
    }

    [Fact]
    public void GetHistory_NeverContainsCancelledSessions()
    {
        var store = new MemoryFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        manager.Start(TimeSpan.FromMinutes(30), "Enfoque", FocusSessionKind.Focus, ReferenceNow);

        manager.Cancel();

        Assert.Empty(manager.GetHistory());
    }

    private static FocusManager CreateManager()
    {
        var manager = new FocusManager(new MemoryFocusStore());
        manager.Load();
        return manager;
    }

    private sealed class MemoryFocusStore : IFocusStore
    {
        public MemoryFocusStore(FocusState? state = null)
        {
            State = state?.Copy() ?? new FocusState();
        }

        public FocusState State { get; private set; }

        public FocusState Load() => State.Copy();

        public void Save(FocusState state)
        {
            State = state.Copy();
        }
    }
}
