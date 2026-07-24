namespace Nexo.Core.Automation;

public sealed class RoutineRunner
{
    private readonly IAutomationActionExecutor _executor;

    public RoutineRunner(IAutomationActionExecutor executor)
    {
        _executor = executor;
    }

    /// <param name="approval">
    /// Evidencia de aprobación para **esta** ejecución. Sin ella, los pasos sensibles se
    /// rechazan. El permiso se aplica aquí, en el ejecutor, y no se confía a que la capa de
    /// interfaz haya preguntado (`SECURITY_MODEL` §Defensa por capas, punto 4).
    /// </param>
    public async Task<RoutineExecutionReport> RunAsync(
        RoutineDefinition routine,
        RoutineExecutionApproval approval = RoutineExecutionApproval.NotConfirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(routine);

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

            if (AutomationPermissionPolicy.GetRisk(action) == AutomationRiskLevel.Sensitive &&
                approval != RoutineExecutionApproval.ConfirmedByUser)
            {
                results.Add(AutomationActionResult.Failed(
                    action,
                    "Confirmación requerida",
                    "Kohana no ejecuta una acción sensible sin que la apruebes primero."));
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
