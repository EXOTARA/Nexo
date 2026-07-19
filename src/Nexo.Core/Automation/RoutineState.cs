namespace Nexo.Core.Automation;

public sealed class RoutineState
{
    public int SchemaVersion { get; set; } = 1;

    public List<RoutineDefinition> Routines { get; set; } = [];

    public RoutineState Copy() => new()
    {
        SchemaVersion = SchemaVersion,
        Routines = Routines.Select(routine => routine.Copy()).ToList()
    };
}
