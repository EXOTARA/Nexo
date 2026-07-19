namespace Nexo.Core.Focus;

public sealed record FocusOperationResult(
    bool Success,
    string Message,
    FocusTimer? Timer = null)
{
    public static FocusOperationResult Completed(
        string message,
        FocusTimer? timer = null) =>
        new(true, message, timer);

    public static FocusOperationResult Failed(string message) =>
        new(false, message);
}
