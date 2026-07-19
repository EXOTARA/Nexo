namespace Nexo.Core.Automation;

public sealed record AutomationActionResult(
    bool Success,
    string Title,
    string Detail,
    AutomationAction Action)
{
    public static AutomationActionResult Completed(
        AutomationAction action,
        string title,
        string detail) =>
        new(true, title, detail, action.Copy());

    public static AutomationActionResult Failed(
        AutomationAction action,
        string title,
        string detail) =>
        new(false, title, detail, action.Copy());
}
