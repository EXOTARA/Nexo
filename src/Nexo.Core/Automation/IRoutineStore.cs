namespace Nexo.Core.Automation;

public interface IRoutineStore
{
    RoutineState Load();

    void Save(RoutineState state);
}
