namespace Nexo.Core.Ai;

public sealed record OllamaOperationResult(bool Success, string Detail)
{
    public static OllamaOperationResult Completed(string detail) => new(true, detail);

    public static OllamaOperationResult Failed(string detail) => new(false, detail);
}
