using Nexo.Core.Automation;

namespace Nexo.Core.Tests;

public sealed class RoutineRunnerTests
{
    [Fact]
    public async Task RunAsync_ContinuesAfterFailure()
    {
        var executor = new FakeExecutor();
        var runner = new RoutineRunner(executor);
        var routine = new RoutineDefinition
        {
            Name = "Prueba",
            TriggerPhrase = "modo prueba",
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.CreateTask,
                    Text = "correcta"
                },
                new AutomationAction
                {
                    Type = AutomationActionType.CreateTask,
                    Text = "fallar"
                },
                new AutomationAction
                {
                    Type = AutomationActionType.StartFocus,
                    NumericValue = 25
                }
            ]
        };

        var report = await runner.RunAsync(routine);

        Assert.Equal(3, report.Results.Count);
        Assert.Equal(2, report.SucceededCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(3, executor.Executed.Count);
    }

    [Fact]
    public async Task RunAsync_SkipsDisabledSteps()
    {
        var executor = new FakeExecutor();
        var runner = new RoutineRunner(executor);
        var routine = new RoutineDefinition
        {
            Name = "Prueba",
            TriggerPhrase = "modo prueba",
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.CreateTask,
                    Text = "omitida",
                    IsEnabled = false
                },
                new AutomationAction
                {
                    Type = AutomationActionType.CreateTask,
                    Text = "correcta"
                }
            ]
        };

        var report = await runner.RunAsync(routine);

        Assert.Single(report.Results);
        Assert.Single(executor.Executed);
    }

    private sealed class FakeExecutor : IAutomationActionExecutor
    {
        public List<AutomationAction> Executed { get; } = [];

        public Task<AutomationActionResult> ExecuteAsync(
            AutomationAction action,
            CancellationToken cancellationToken)
        {
            Executed.Add(action.Copy());
            return Task.FromResult(action.Text == "fallar"
                ? AutomationActionResult.Failed(action, "Falló", "Fallo intencional")
                : AutomationActionResult.Completed(action, "Listo", "Acción completada"));
        }
    }
}
