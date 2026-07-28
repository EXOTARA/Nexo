using Nexo.Core.Ambient;

namespace Nexo.Core.Tests;

public sealed class AmbientRequestManagerTests
{
    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 28, 10, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void Begin_PersistsListeningRequest()
    {
        var store = new MemoryAmbientRequestHistoryStore();
        var manager = new AmbientRequestManager(store);
        manager.Load();

        var result = manager.Begin("¿qué hora es?", context: null, ReferenceNow);

        Assert.True(result.Success);
        Assert.NotNull(store.State.ActiveRequest);
        Assert.Equal(AmbientRequestStatus.Listening, store.State.ActiveRequest?.Status);
    }

    [Fact]
    public void Begin_RejectsBlank()
    {
        var manager = CreateManager();

        var result = manager.Begin("   ", context: null, ReferenceNow);

        Assert.False(result.Success);
    }

    [Fact]
    public void Begin_RejectsSecondRequestWhileOneIsRunning()
    {
        var manager = CreateManager();
        manager.Begin("primera", context: null, ReferenceNow);

        var result = manager.Begin("segunda", context: null, ReferenceNow.AddSeconds(1));

        Assert.False(result.Success);
    }

    [Fact]
    public void Begin_ArchivesPreviousTerminalRequestAutomatically()
    {
        var manager = CreateManager();
        manager.Begin("primera", context: null, ReferenceNow);
        manager.Cancel(ReferenceNow.AddSeconds(1));

        var result = manager.Begin("segunda", context: null, ReferenceNow.AddSeconds(2));

        Assert.True(result.Success);
        Assert.Single(manager.GetHistory());
    }

    [Fact]
    public void FullLifecycle_ListeningThinkingResult()
    {
        var manager = CreateManager();
        manager.Begin("resume esto", context: null, ReferenceNow);

        var thinking = manager.BeginThinking(ReferenceNow.AddSeconds(1));
        Assert.True(thinking.Success);
        Assert.Equal(AmbientRequestStatus.Thinking, thinking.Request?.Status);

        var result = new AmbientRequestResult(
            "Resumen corto",
            "Resumen expandido con más detalle.",
            [new AmbientQuickAction("copy", "Copiar", AmbientAutonomyLevel.Ver)],
            CanUndo: false);

        var completed = manager.CompleteWithResult(result, ReferenceNow.AddSeconds(2));

        Assert.True(completed.Success);
        Assert.Equal(AmbientRequestStatus.Result, completed.Request?.Status);
        Assert.Equal("Resumen corto", completed.Request?.Result?.ShortText);
    }

    [Fact]
    public void CompleteWithResult_FailsWithoutActiveRequest()
    {
        var manager = CreateManager();

        var result = manager.CompleteWithResult(
            new AmbientRequestResult("x", null, [], CanUndo: false),
            ReferenceNow);

        Assert.False(result.Success);
    }

    [Fact]
    public void Fail_RecordsErrorAndKeepsRequestVisible()
    {
        var manager = CreateManager();
        manager.Begin("algo que falla", context: null, ReferenceNow);

        var result = manager.Fail("no se pudo procesar", ReferenceNow.AddSeconds(1));

        Assert.True(result.Success);
        Assert.Equal(AmbientRequestStatus.Failed, result.Request?.Status);
        Assert.Equal("no se pudo procesar", result.Request?.ErrorMessage);
    }

    [Fact]
    public void Cancel_ArchivesAsCancelledAndClearsActive()
    {
        var manager = CreateManager();
        manager.Begin("algo", context: null, ReferenceNow);

        var result = manager.Cancel(ReferenceNow.AddSeconds(1));

        Assert.True(result.Success);
        var history = manager.GetHistory();
        Assert.Single(history);
        Assert.Equal(AmbientRequestStatus.Cancelled, history[0].Status);
        Assert.Null(manager.GetSnapshot().ActiveRequest);
    }

    [Fact]
    public void Cancel_FailsWithoutActiveRequest()
    {
        var manager = CreateManager();

        var result = manager.Cancel(ReferenceNow);

        Assert.False(result.Success);
    }

    [Fact]
    public void Dismiss_RejectsWhileStillRunning()
    {
        var manager = CreateManager();
        manager.Begin("algo", context: null, ReferenceNow);

        var result = manager.Dismiss(ReferenceNow.AddSeconds(1));

        Assert.False(result.Success);
        Assert.NotNull(manager.GetSnapshot().ActiveRequest);
    }

    [Fact]
    public void Dismiss_ArchivesTerminalRequest()
    {
        var manager = CreateManager();
        manager.Begin("algo", context: null, ReferenceNow);
        manager.Fail("error", ReferenceNow.AddSeconds(1));

        var result = manager.Dismiss(ReferenceNow.AddSeconds(2));

        Assert.True(result.Success);
        Assert.Null(manager.GetSnapshot().ActiveRequest);
        Assert.Single(manager.GetHistory());
    }

    [Fact]
    public void Undo_OnActiveResult_MarksUndoneOnlyWhenAllowed()
    {
        var manager = CreateManager();
        manager.Begin("algo reversible", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow.AddSeconds(1));
        var request = manager.CompleteWithResult(
            new AmbientRequestResult("hecho", null, [], CanUndo: true),
            ReferenceNow.AddSeconds(2)).Request!;

        var undo = manager.Undo(request.Id, ReferenceNow.AddSeconds(3));
        var secondUndo = manager.Undo(request.Id, ReferenceNow.AddSeconds(4));

        Assert.True(undo.Success);
        Assert.False(secondUndo.Success);
    }

    [Fact]
    public void Undo_RejectsWhenResultCannotBeUndone()
    {
        var manager = CreateManager();
        manager.Begin("algo no reversible", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow.AddSeconds(1));
        var request = manager.CompleteWithResult(
            new AmbientRequestResult("hecho", null, [], CanUndo: false),
            ReferenceNow.AddSeconds(2)).Request!;

        var undo = manager.Undo(request.Id, ReferenceNow.AddSeconds(3));

        Assert.False(undo.Success);
    }

    [Fact]
    public void Undo_OnArchivedHistoryEntry_Works()
    {
        var manager = CreateManager();
        manager.Begin("algo reversible", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow.AddSeconds(1));
        manager.CompleteWithResult(
            new AmbientRequestResult("hecho", null, [], CanUndo: true),
            ReferenceNow.AddSeconds(2));
        var archivedId = manager.GetSnapshot().ActiveRequest!.Id;
        manager.Dismiss(ReferenceNow.AddSeconds(3));

        var undo = manager.Undo(archivedId, ReferenceNow.AddSeconds(4));

        Assert.True(undo.Success);
        Assert.True(manager.GetHistory().Single().Undone);
    }

    [Fact]
    public void Undo_FailsWhenRequestIsUnknown()
    {
        var manager = CreateManager();

        var result = manager.Undo(Guid.NewGuid(), ReferenceNow);

        Assert.False(result.Success);
    }

    [Fact]
    public void GetSnapshot_ReturnsRecentHistoryNewestFirst()
    {
        var manager = CreateManager();
        manager.Begin("una", context: null, ReferenceNow);
        manager.Cancel(ReferenceNow.AddSeconds(1));
        manager.Begin("dos", context: null, ReferenceNow.AddSeconds(2));
        manager.Cancel(ReferenceNow.AddSeconds(3));

        var snapshot = manager.GetSnapshot(recentCount: 1);

        Assert.Single(snapshot.RecentHistory);
        Assert.Equal("dos", snapshot.RecentHistory[0].Prompt);
    }

    [Fact]
    public void Context_IsPreservedOnActiveRequest()
    {
        var manager = CreateManager();
        var context = new AmbientContextSnapshot("Bloc de notas", "notepad", IsSensitive: false);

        manager.Begin("resume esta ventana", context, ReferenceNow);

        Assert.Equal(context, manager.GetSnapshot().ActiveRequest?.Context);
    }

    private static AmbientRequestManager CreateManager()
    {
        var manager = new AmbientRequestManager(new MemoryAmbientRequestHistoryStore());
        manager.Load();
        return manager;
    }

    private sealed class MemoryAmbientRequestHistoryStore : IAmbientRequestHistoryStore
    {
        public MemoryAmbientRequestHistoryStore(AmbientRequestState? state = null)
        {
            State = state?.Copy() ?? new AmbientRequestState();
        }

        public AmbientRequestState State { get; private set; }

        public AmbientRequestState Load() => State.Copy();

        public void Save(AmbientRequestState state)
        {
            State = state.Copy();
        }
    }
}
