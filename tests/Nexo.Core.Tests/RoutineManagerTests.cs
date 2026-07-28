using Nexo.Core.Automation;

namespace Nexo.Core.Tests;

public sealed class RoutineManagerTests
{
    [Fact]
    public void Load_EmptyStore_CreatesDefaultRoutines()
    {
        var store = new MemoryRoutineStore();
        var manager = new RoutineManager(store);

        manager.Load();

        Assert.Contains(manager.GetAll(), routine => routine.Name == "Programación");
        Assert.Contains(manager.GetAll(), routine => routine.TriggerPhrase == "modo estudio");
        Assert.NotNull(store.State);
    }

    [Fact]
    public void FindBestMatch_IgnoresAccentsAndMatchesTrigger()
    {
        var manager = new RoutineManager(new MemoryRoutineStore());
        manager.Load();

        var routine = manager.FindBestMatch("modo programacion");

        Assert.NotNull(routine);
        Assert.Equal("Programación", routine.Name);
    }

    [Fact]
    public void Create_ValidRoutine_PersistsCopy()
    {
        var store = new MemoryRoutineStore();
        var manager = new RoutineManager(store);
        manager.Load();
        var result = manager.Create(new RoutineDefinition
        {
            Name = "Lectura",
            TriggerPhrase = "modo lectura",
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.StartFocus,
                    NumericValue = 30
                }
            ]
        });

        Assert.True(result.Success);
        Assert.NotNull(manager.FindBestMatch("modo lectura"));
        Assert.Contains(store.State!.Routines, routine => routine.Name == "Lectura");
    }

    [Fact]
    public void Create_EmptySteps_IsRejected()
    {
        var manager = new RoutineManager(new MemoryRoutineStore());
        manager.Load();

        var result = manager.Create(new RoutineDefinition
        {
            Name = "Vacía",
            TriggerPhrase = "modo vacio"
        });

        Assert.False(result.Success);
    }

    [Fact]
    public void Delete_RemovesRoutine()
    {
        var manager = new RoutineManager(new MemoryRoutineStore());
        manager.Load();
        var routine = manager.GetAll().First();

        var result = manager.Delete(routine.Id);

        Assert.True(result.Success);
        Assert.DoesNotContain(manager.GetAll(), candidate => candidate.Id == routine.Id);
    }

    // ---------- Diseño D3: alternar habilitada/deshabilitada ----------

    [Fact]
    public void SetEnabled_False_DisablesTheRoutine()
    {
        var manager = new RoutineManager(new MemoryRoutineStore());
        manager.Load();
        var routine = manager.GetAll().First();

        var result = manager.SetEnabled(routine.Id, false);

        Assert.True(result.Success);
        Assert.False(manager.GetAll().Single(candidate => candidate.Id == routine.Id).IsEnabled);
    }

    [Fact]
    public void SetEnabled_True_ReenablesTheRoutine()
    {
        var manager = new RoutineManager(new MemoryRoutineStore());
        manager.Load();
        var routine = manager.GetAll().First();
        manager.SetEnabled(routine.Id, false);

        manager.SetEnabled(routine.Id, true);

        Assert.True(manager.GetAll().Single(candidate => candidate.Id == routine.Id).IsEnabled);
    }

    [Fact]
    public void SetEnabled_Persists()
    {
        var store = new MemoryRoutineStore();
        var manager = new RoutineManager(store);
        manager.Load();
        var routine = manager.GetAll().First();

        manager.SetEnabled(routine.Id, false);

        Assert.False(store.State!.Routines.Single(candidate => candidate.Id == routine.Id).IsEnabled);
    }

    [Fact]
    public void SetEnabled_OnMissingRoutine_Fails()
    {
        var manager = new RoutineManager(new MemoryRoutineStore());
        manager.Load();

        var result = manager.SetEnabled(Guid.NewGuid(), true);

        Assert.False(result.Success);
    }

    // ---------- Diseño D3: registrar el resultado de una ejecución ----------

    [Fact]
    public void RecordExecution_SetsLastExecutedAtAndOutcome()
    {
        var manager = new RoutineManager(new MemoryRoutineStore());
        manager.Load();
        var routine = manager.GetAll().First();
        var completedAt = new DateTimeOffset(2026, 7, 25, 9, 0, 0, TimeSpan.FromHours(-6));

        manager.RecordExecution(routine.Id, completedAt, succeeded: true);

        var updated = manager.GetAll().Single(candidate => candidate.Id == routine.Id);
        Assert.Equal(completedAt, updated.LastExecutedAt);
        Assert.True(updated.LastExecutionSucceeded);
    }

    [Fact]
    public void RecordExecution_WithFailure_RecordsFalse()
    {
        var manager = new RoutineManager(new MemoryRoutineStore());
        manager.Load();
        var routine = manager.GetAll().First();

        manager.RecordExecution(routine.Id, DateTimeOffset.Now, succeeded: false);

        Assert.False(manager.GetAll().Single(candidate => candidate.Id == routine.Id).LastExecutionSucceeded);
    }

    [Fact]
    public void RecordExecution_OnMissingRoutine_DoesNotThrow()
    {
        var manager = new RoutineManager(new MemoryRoutineStore());
        manager.Load();

        var exception = Record.Exception(() => manager.RecordExecution(Guid.NewGuid(), DateTimeOffset.Now, true));

        Assert.Null(exception);
    }

    [Fact]
    public void NewRoutine_HasNoExecutionHistoryUntilRun()
    {
        var manager = new RoutineManager(new MemoryRoutineStore());
        manager.Load();

        var routine = manager.GetAll().First();

        Assert.Null(routine.LastExecutedAt);
        Assert.Null(routine.LastExecutionSucceeded);
    }

    // ---------- Diseño D3: migración de esquema v1 → v2 ----------

    [Fact]
    public void Load_MigratesSchemaVersion1To2_OnceSomethingIsSavedAgain()
    {
        // Normalize() migra en memoria al cargar, pero —igual que ShellPreferences.Normalize()—
        // no fuerza una escritura a disco solo por leer: la versión persistida se pone al día en
        // la siguiente operación que ya guarda por su cuenta. Esa es la garantía real, no que
        // Load() por sí solo reescriba el archivo.
        var store = new MemoryRoutineStore();
        store.Save(new RoutineState
        {
            SchemaVersion = 1,
            Routines =
            [
                new RoutineDefinition
                {
                    Name = "Existente",
                    TriggerPhrase = "modo existente",
                    Steps = [new AutomationAction { Type = AutomationActionType.StartFocus, NumericValue = 20 }]
                }
            ]
        });
        var manager = new RoutineManager(store);
        manager.Load();

        manager.SetEnabled(manager.GetAll().Single().Id, true);

        Assert.Equal(2, store.State!.SchemaVersion);
    }

    [Fact]
    public void Load_MigratingFromV1_KeepsExistingRoutinesIntact()
    {
        var store = new MemoryRoutineStore();
        store.Save(new RoutineState
        {
            SchemaVersion = 1,
            Routines =
            [
                new RoutineDefinition
                {
                    Name = "Existente",
                    TriggerPhrase = "modo existente",
                    IsEnabled = false,
                    Steps = [new AutomationAction { Type = AutomationActionType.StartFocus, NumericValue = 20 }]
                }
            ]
        });
        var manager = new RoutineManager(store);

        manager.Load();

        var routine = manager.GetAll().Single(candidate => candidate.Name == "Existente");
        Assert.False(routine.IsEnabled);
        Assert.Null(routine.LastExecutedAt);
    }

    private sealed class MemoryRoutineStore : IRoutineStore
    {
        public RoutineState? State { get; private set; }

        public RoutineState Load() => State?.Copy() ?? new RoutineState();

        public void Save(RoutineState state) => State = state.Copy();
    }
}
