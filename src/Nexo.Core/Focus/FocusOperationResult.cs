namespace Nexo.Core.Focus;

public sealed record FocusOperationResult(
    bool Success,
    string Message,
    FocusTimer? Timer = null,
    FocusCompletion? Completion = null)
{
    public static FocusOperationResult Completed(
        string message,
        FocusTimer? timer = null,
        FocusCompletion? completion = null) =>
        new(true, message, timer, completion);

    public static FocusOperationResult Failed(string message) =>
        new(false, message);
}
