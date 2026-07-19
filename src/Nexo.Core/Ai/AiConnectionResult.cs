namespace Nexo.Core.Ai;

public sealed record AiConnectionResult(
    bool IsSuccess,
    string Detail,
    IReadOnlyList<string> Models)
{
    public static AiConnectionResult Success(
        string detail,
        IReadOnlyList<string> models) =>
        new(true, detail, models);

    public static AiConnectionResult Failed(string detail) =>
        new(false, detail, Array.Empty<string>());
}
