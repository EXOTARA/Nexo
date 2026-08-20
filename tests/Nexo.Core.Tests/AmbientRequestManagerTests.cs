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

    // ---------- Diseño D7: respuesta por partes ----------

    [Fact]
    public void Streaming_ShowsTextAsItArrivesAndCompletesWithTheWholeAnswer()
    {
        var manager = CreateManager();
        manager.Begin("resume esto", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow.AddSeconds(1));

        Assert.True(manager.BeginStreaming(ReferenceNow.AddSeconds(2)).Success);
        manager.AppendStreamedText("Hola", ReferenceNow.AddSeconds(3));
        manager.AppendStreamedText(" mundo", ReferenceNow.AddSeconds(4));

        var midway = manager.GetSnapshot().ActiveRequest!;
        Assert.Equal(AmbientRequestStatus.Streaming, midway.Status);
        Assert.Equal("Hola mundo", midway.PartialText);

        var completed = manager.CompleteStreamedResult([], canUndo: false, ReferenceNow.AddSeconds(5));

        Assert.True(completed.Success);
        Assert.Equal(AmbientRequestStatus.Result, completed.Request?.Status);
        Assert.Equal("Hola mundo", completed.Request?.Result?.ExpandedText);
    }

    [Fact]
    public void Streaming_UsesTheSummarizerForTheShortTextWhenGivenOne()
    {
        var manager = CreateManager();
        manager.Begin("resume esto", context: null, ReferenceNow);
        manager.BeginStreaming(ReferenceNow.AddSeconds(1));
        manager.AppendStreamedText("respuesta larga", ReferenceNow.AddSeconds(2));

        var completed = manager.CompleteStreamedResult(
            [], canUndo: false, ReferenceNow.AddSeconds(3), _ => "corto");

        Assert.Equal("corto", completed.Request?.Result?.ShortText);
        Assert.Equal("respuesta larga", completed.Request?.Result?.ExpandedText);
    }

    [Fact]
    public void Streaming_ThatNeverProducedTextFailsInsteadOfShowingAnEmptyResult()
    {
        var manager = CreateManager();
        manager.Begin("algo", context: null, ReferenceNow);
        manager.BeginStreaming(ReferenceNow.AddSeconds(1));

        var completed = manager.CompleteStreamedResult([], canUndo: false, ReferenceNow.AddSeconds(2));

        Assert.False(completed.Success);
        Assert.Equal(AmbientRequestStatus.Failed, manager.GetSnapshot().ActiveRequest?.Status);
    }

    [Fact]
    public void AppendStreamedText_OutsideAStreamIsRejected()
    {
        var manager = CreateManager();
        manager.Begin("algo", context: null, ReferenceNow);

        Assert.False(manager.AppendStreamedText("hola", ReferenceNow.AddSeconds(1)).Success);
    }

    [Fact]
    public void Streaming_IsStillInFlight_SoANewRequestIsRejectedAndDismissRefuses()
    {
        var manager = CreateManager();
        manager.Begin("primera", context: null, ReferenceNow);
        manager.BeginStreaming(ReferenceNow.AddSeconds(1));

        Assert.False(manager.Begin("segunda", context: null, ReferenceNow.AddSeconds(2)).Success);
        Assert.False(manager.Dismiss(ReferenceNow.AddSeconds(3)).Success);
    }

    [Fact]
    public void Streaming_CanBeCancelledMidAnswer()
    {
        var manager = CreateManager();
        manager.Begin("algo", context: null, ReferenceNow);
        manager.BeginStreaming(ReferenceNow.AddSeconds(1));
        manager.AppendStreamedText("a medias", ReferenceNow.AddSeconds(2));

        Assert.True(manager.Cancel(ReferenceNow.AddSeconds(3)).Success);
        Assert.Null(manager.GetSnapshot().ActiveRequest);
    }

    [Fact]
    public void FailIfStalled_ClosesARequestThatNeverGotAnAnswer()
    {
        // Diseño D63 — el fallo que Adler reportó: Lens se quedaba en "Pensando…" para siempre. El
        // corte del cliente de IA llega como cancelación desde dentro de un iterador y se escapaba
        // de los filtros de excepción, así que nadie cerraba la solicitud.
        var manager = CreateManager();
        manager.Begin("Sakura Lens — modo estudio", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow);

        var result = manager.FailIfStalled(ReferenceNow + AmbientRequestManager.ThinkingStallLimit);

        Assert.True(result.Success);
        Assert.Equal(AmbientRequestStatus.Failed, manager.GetSnapshot().ActiveRequest?.Status);
    }

    [Fact]
    public void FailIfStalled_LeavesASlowRequestAlone()
    {
        // Un modelo local sobre una gráfica modesta tarda minutos en soltar el primer fragmento, y
        // eso es lentitud, no un cuelgue. El plazo se cumple o no se toca nada.
        var manager = CreateManager();
        manager.Begin("Sakura Lens — modo soporte", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow);

        var result = manager.FailIfStalled(
            ReferenceNow + AmbientRequestManager.ThinkingStallLimit - TimeSpan.FromSeconds(1));

        Assert.False(result.Success);
        Assert.Equal(AmbientRequestStatus.Thinking, manager.GetSnapshot().ActiveRequest?.Status);
    }

    [Fact]
    public void FailIfStalled_KeepsWhatArrivedWhenTheStreamGoesQuiet()
    {
        // La regla de D7: media respuesta es más que ninguna. Si ya había texto, la solicitud se
        // cierra COMO RESULTADO y no como fallo, con la nota de que se cortó.
        var manager = CreateManager();
        manager.Begin("Sakura Lens — modo desarrollo", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow);
        manager.BeginStreaming(ReferenceNow);
        manager.AppendStreamedText("La ventana muestra", ReferenceNow);

        var result = manager.FailIfStalled(
            ReferenceNow + AmbientRequestManager.StreamingSilenceLimit);

        Assert.True(result.Success);

        var active = manager.GetSnapshot().ActiveRequest;
        Assert.Equal(AmbientRequestStatus.Result, active?.Status);
        Assert.Equal("La ventana muestra", active?.Result?.ExpandedText);
        Assert.NotNull(active?.ErrorMessage);
    }

    [Fact]
    public void FailIfStalled_CountsSilenceFromTheLastChunkAndNotFromTheStart()
    {
        // Una respuesta larga puede tardar más que el plazo entero sin estar colgada: lo que se
        // mide es el silencio, no la duración.
        var manager = CreateManager();
        manager.Begin("Sakura Lens — modo estudio", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow);
        manager.BeginStreaming(ReferenceNow);

        var late = ReferenceNow + AmbientRequestManager.StreamingSilenceLimit + TimeSpan.FromMinutes(5);
        manager.AppendStreamedText("sigue escribiendo", late);

        var result = manager.FailIfStalled(late + TimeSpan.FromSeconds(30));

        Assert.False(result.Success);
        Assert.Equal(AmbientRequestStatus.Streaming, manager.GetSnapshot().ActiveRequest?.Status);
    }

    [Fact]
    public void FailIfStalled_DoesNotTouchARequestThatAlreadyFinished()
    {
        var manager = CreateManager();
        manager.Begin("¿qué ventana tengo activa?", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow);
        manager.CompleteWithResult(
            new AmbientRequestResult("Explorador", "Explorador de Windows", [], CanUndo: false),
            ReferenceNow);

        var result = manager.FailIfStalled(ReferenceNow + TimeSpan.FromHours(2));

        Assert.False(result.Success);
        Assert.Equal(AmbientRequestStatus.Result, manager.GetSnapshot().ActiveRequest?.Status);
    }

    [Fact]
    public void Load_ClosesARequestLeftRunningByAPreviousProcess()
    {
        // Diseño D63 — el fallo de verdad, encontrado leyendo el archivo de estado del equipo de
        // Adler: una solicitud en "Escuchando" del 2 de agosto, dieciséis días bloqueando cada
        // orden de Lens con "ya hay una solicitud ambiental en curso". Si Sakura se cierra a mitad,
        // nadie va a terminar esa solicitud al arrancar de nuevo: quien iba a hacerlo ya no existe.
        var store = new MemoryAmbientRequestHistoryStore();
        store.State.ActiveRequest = new AmbientRequest
        {
            Prompt = "solicitud",
            Status = AmbientRequestStatus.Listening,
            CreatedAt = ReferenceNow,
            UpdatedAt = ReferenceNow
        };

        var manager = new AmbientRequestManager(store);
        manager.Load();

        Assert.Null(manager.GetSnapshot().ActiveRequest);
        Assert.Single(manager.GetHistory());
        Assert.Equal(AmbientRequestStatus.Failed, manager.GetHistory()[0].Status);

        // Y lo que importa de verdad: se puede volver a pedir algo.
        Assert.True(manager.Begin("Sakura Lens — modo estudio", context: null, ReferenceNow).Success);
    }

    [Fact]
    public void Load_LeavesAFinishedRequestOnScreen()
    {
        // Un resultado sin descartar sí sobrevive al reinicio: se leyó o no se leyó, pero no
        // bloquea nada y borrarlo sería tirar lo que la persona todavía no ha visto.
        var store = new MemoryAmbientRequestHistoryStore();
        store.State.ActiveRequest = new AmbientRequest
        {
            Prompt = "¿qué ventana tengo activa?",
            Status = AmbientRequestStatus.Result,
            CreatedAt = ReferenceNow,
            UpdatedAt = ReferenceNow,
            Result = new AmbientRequestResult("Explorador", null, [], CanUndo: false)
        };

        var manager = new AmbientRequestManager(store);
        manager.Load();

        Assert.NotNull(manager.GetSnapshot().ActiveRequest);
        Assert.Empty(manager.GetHistory());
    }

    [Fact]
    public void FailIfStalled_ClosesARequestThatNeverLeftListening()
    {
        // Escuchando dura milisegundos: es el hueco entre Begin y el primer paso asíncrono. Si
        // alguien se va por una rama de error sin cerrarla, ahí se queda, y Begin rechaza todo lo
        // que venga después.
        var manager = CreateManager();
        manager.Begin("Sakura Lens — modo soporte", context: null, ReferenceNow);

        var result = manager.FailIfStalled(ReferenceNow + AmbientRequestManager.ListeningStallLimit);

        Assert.True(result.Success);
        Assert.Equal(AmbientRequestStatus.Failed, manager.GetSnapshot().ActiveRequest?.Status);
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
