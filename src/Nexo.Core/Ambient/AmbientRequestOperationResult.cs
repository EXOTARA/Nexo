namespace Nexo.Core.Ambient;

public sealed record AmbientRequestOperationResult(
    bool Success,
    string Message,
    AmbientRequest? Request = null)
{
    public static AmbientRequestOperationResult Completed(
        string message,
        AmbientRequest? request = null) =>
        new(true, message, request);

    public static AmbientRequestOperationResult Failed(string message) =>
        new(false, message);
}
