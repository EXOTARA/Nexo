namespace Nexo.Core.Automation;

public interface IAutomationActionExecutor
{
    Task<AutomationActionResult> ExecuteAsync(
        AutomationAction action,
        CancellationToken cancellationToken);
}
