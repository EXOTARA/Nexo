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

    private sealed class MemoryRoutineStore : IRoutineStore
    {
        public RoutineState? State { get; private set; }

        public RoutineState Load() => State?.Copy() ?? new RoutineState();

        public void Save(RoutineState state) => State = state.Copy();
    }
}
