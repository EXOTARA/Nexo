namespace Nexo.Core.Audio;

public sealed record AudioActionResult(
    AudioActionStatus Status,
    string Title,
    string Detail,
    double? VolumePercent = null)
{
    public bool Succeeded => Status == AudioActionStatus.Success;

    public static AudioActionResult Success(
        string title,
        string detail,
        double? volumePercent = null) =>
        new(AudioActionStatus.Success, title, detail, volumePercent);

    public static AudioActionResult NotFound(string target) =>
        new(
            AudioActionStatus.NotFound,
            "Aplicación no encontrada",
            $"No encontré una sesión de audio para {target}.");

    public static AudioActionResult Unavailable(string detail) =>
        new(AudioActionStatus.Unavailable, "Audio no disponible", detail);

    public static AudioActionResult Failed(string detail) =>
        new(AudioActionStatus.Failed, "No se pudo cambiar el audio", detail);
}
