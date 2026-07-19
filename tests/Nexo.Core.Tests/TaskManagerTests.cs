using Nexo.Core.Tasks;

namespace Nexo.Core.Tests;

public sealed class TaskManagerTests
{
    [Fact]
    public void Create_PersistsTask()
    {
        var store = new MemoryTaskStore();
        var manager = new TaskManager(store);
        manager.Load();

        var created = manager.Create("Revisar proyecto");

        Assert.Equal("Revisar proyecto", created.Title);
        Assert.Single(store.Tasks);
    }

    [Fact]
    public void CompleteMatching_IgnoresAccentsAndCase()
    {
        var store = new MemoryTaskStore([
            new NexoTask { Title = "Revisión de matrices" }
        ]);
        var manager = new TaskManager(store);
        manager.Load();

        var result = manager.CompleteMatching("revision de matrices");

        Assert.True(result.Success);
        Assert.True(manager.GetAll().Single().IsCompleted);
    }

    [Fact]
    public void DeleteMatching_RemovesTask()
    {
        var store = new MemoryTaskStore([
            new NexoTask { Title = "Comprar alcohol" }
        ]);
        var manager = new TaskManager(store);
        manager.Load();

        var result = manager.DeleteMatching("comprar alcohol");

        Assert.True(result.Success);
        Assert.Empty(manager.GetAll());
    }

    [Fact]
    public void CollectDueReminders_DeliversOnlyOnce()
    {
        var now = DateTimeOffset.Now;
        var store = new MemoryTaskStore([
            new NexoTask
            {
                Title = "Entregar tarea",
                DueAt = now.AddMinutes(-1),
                ReminderEnabled = true
            }
        ]);
        var manager = new TaskManager(store);
        manager.Load();

        var first = manager.CollectDueReminders(now);
        var second = manager.CollectDueReminders(now.AddMinutes(1));

        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public void CollectDueReminders_SkipsCompletedTasks()
    {
        var now = DateTimeOffset.Now;
        var store = new MemoryTaskStore([
            new NexoTask
            {
                Title = "Tarea hecha",
                DueAt = now.AddMinutes(-1),
                ReminderEnabled = true,
                CompletedAt = now.AddMinutes(-2)
            }
        ]);
        var manager = new TaskManager(store);
        manager.Load();

        Assert.Empty(manager.CollectDueReminders(now));
    }

    [Fact]
    public void BuildTodaySummary_ReportsTodayAndOverdueTasks()
    {
        var now = DateTimeOffset.Now;
        var store = new MemoryTaskStore([
            new NexoTask { Title = "Tarea de hoy", DueAt = now.AddHours(1) },
            new NexoTask { Title = "Tarea vencida", DueAt = now.AddDays(-1) }
        ]);
        var manager = new TaskManager(store);
        manager.Load();

        var summary = manager.BuildTodaySummary(now);

        Assert.Contains("Tarea de hoy", summary);
        Assert.Contains("vencida", summary.ToLowerInvariant());
    }

    private sealed class MemoryTaskStore : ITaskStore
    {
        public MemoryTaskStore(IEnumerable<NexoTask>? tasks = null)
        {
            Tasks = tasks?.Select(task => task.Copy()).ToList() ?? [];
        }

        public List<NexoTask> Tasks { get; private set; }

        public IReadOnlyList<NexoTask> Load() =>
            Tasks.Select(task => task.Copy()).ToArray();

        public void Save(IReadOnlyCollection<NexoTask> tasks)
        {
            Tasks = tasks.Select(task => task.Copy()).ToList();
        }
    }
}
