namespace Nexo.Core.Automation;

public sealed class RoutineRunner
{
    private readonly IAutomationActionExecutor _executor;

    public RoutineRunner(IAutomationActionExecutor executor)
    {
        _executor = executor;
    }

    public async Task<RoutineExecutionReport> RunAsync(
        RoutineDefinition routine,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        var results = new List<AutomationActionResult>();

        foreach (var action in routine.Steps.Where(step => step.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!AutomationPermissionPolicy.IsAllowed(action, out var validationError))
            {
                results.Add(AutomationActionResult.Failed(
                    action,
                    "Acción bloqueada",
                    validationError));
                continue;
            }

            try
            {
                results.Add(await _executor.ExecuteAsync(action, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                results.Add(AutomationActionResult.Failed(
                    action,
                    "La acción falló",
                    exception.Message));
            }
        }

        return new RoutineExecutionReport(
            routine.Copy(),
            results,
            startedAt,
            DateTimeOffset.Now);
    }
}
