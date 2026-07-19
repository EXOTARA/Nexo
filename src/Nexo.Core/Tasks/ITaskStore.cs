namespace Nexo.Core.Tasks;

public interface ITaskStore
{
    IReadOnlyList<NexoTask> Load();

    void Save(IReadOnlyCollection<NexoTask> tasks);
}
