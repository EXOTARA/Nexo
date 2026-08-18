namespace Nexo.Core.Display;

/// <summary>
/// El brillo del monitor tal como está ahora. <paramref name="IsAvailable"/> en falso no es un
/// error: la mayoría de monitores externos no exponen el brillo por cable, y eso es normal.
/// </summary>
public sealed record BrightnessSnapshot(
    bool IsAvailable,
    int Percent,
    string? ErrorMessage = null)
{
    public static BrightnessSnapshot Unavailable(string message) =>
        new(false, 0, message);
}

public interface IDisplayBrightnessService
{
    BrightnessSnapshot ReadSnapshot();

    bool TrySetBrightness(int percent);
}
