namespace Nexo.Core.Ai;

public sealed record OllamaPullProgress(
    string Status,
    long? Completed,
    long? Total)
{
    public double? Percentage =>
        Completed.HasValue && Total is > 0
            ? Math.Clamp(Completed.Value * 100d / Total.Value, 0d, 100d)
            : null;
}
