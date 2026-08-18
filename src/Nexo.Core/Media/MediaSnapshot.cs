namespace Nexo.Core.Media;

/// <summary>
/// Diseño D43 — lo que suena ahora mismo en el equipo, sin depender de quién lo reproduce.
///
/// La portada viaja como bytes y no como imagen ya construida porque este proyecto no conoce WPF:
/// quien la pinte decidirá el formato. <see cref="Nothing"/> es un estado legítimo y no un error —
/// «no hay nada sonando» se enseña, no se oculta.
/// </summary>
public sealed record MediaSnapshot(
    bool HasSession,
    string Title,
    string Artist,
    string Album,
    string SourceApp,
    bool IsPlaying,
    bool CanPlayPause,
    bool CanGoNext,
    bool CanGoPrevious,
    TimeSpan? Position,
    TimeSpan? Duration,

    /// <summary>
    /// Instante en que <paramref name="Position"/> fue cierta, segun el propio reproductor.
    ///
    /// Sin este dato la posicion no sirve para dibujar una barra que avance: Windows la refresca
    /// cuando al reproductor le apetece, no continuamente. Con el, la posicion de ahora se calcula.
    /// </summary>
    DateTimeOffset? PositionUpdatedAt,
    byte[]? Thumbnail)
{
    public static readonly MediaSnapshot Nothing = new(
        HasSession: false,
        Title: string.Empty,
        Artist: string.Empty,
        Album: string.Empty,
        SourceApp: string.Empty,
        IsPlaying: false,
        CanPlayPause: false,
        CanGoNext: false,
        CanGoPrevious: false,
        Position: null,
        Duration: null,
        PositionUpdatedAt: null,
        Thumbnail: null);

    /// <summary>
    /// Dos lecturas describen la misma pantalla cuando coinciden en todo menos en la posición y en
    /// la portada. Sirve para no reconstruir la carátula —lo caro— dos veces por segundo mientras
    /// suena la misma canción.
    /// </summary>
    public bool SameTrackAs(MediaSnapshot other) =>
        other is not null &&
        HasSession == other.HasSession &&
        Title == other.Title &&
        Artist == other.Artist &&
        Album == other.Album &&
        SourceApp == other.SourceApp;
}
