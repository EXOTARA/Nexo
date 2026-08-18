namespace Nexo.Core.Media;

/// <summary>
/// Diseño D43 — lo que suena en el equipo y los tres controles que Kohana puede accionar.
///
/// Los controles devuelven si la orden se aceptó, no si el reproductor obedeció: entre pulsar
/// «siguiente» y que Spotify cambie de pista hay una ida y vuelta que no ocurre dentro de esta
/// llamada.
/// </summary>
public interface IMediaSessionReader
{
    Task<MediaSnapshot> ReadAsync(CancellationToken cancellationToken = default);

    Task<bool> TogglePlayPauseAsync();

    Task<bool> SkipNextAsync();

    Task<bool> SkipPreviousAsync();
}
