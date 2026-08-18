using System.Runtime.InteropServices;
using Nexo.Core.Media;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Nexo.Windows.Media;

/// <summary>
/// Diseño D43 — lee la sesión de medios de Windows: la misma que alimenta el recuadro que sale al
/// subir el volumen. Sirve para Spotify, para un vídeo en el navegador y para cualquier programa
/// que se anuncie al sistema, sin integrarse con ninguno en particular.
///
/// El gestor se pide una sola vez y se guarda. Pedirlo en cada lectura —dos veces por segundo—
/// cuesta un salto a WinRT que no hace falta, y además devuelve un objeto nuevo cada vez.
///
/// La portada se lee solo cuando cambia la pista. Es lo único caro de aquí: abrir el flujo,
/// copiarlo a memoria y decodificarlo. Repetirlo en cada tick por una imagen idéntica se notaba en
/// el uso de CPU del propio Kohana, que es justo lo que esta pantalla dice medir.
/// </summary>
public sealed class WindowsMediaSessionReader : IMediaSessionReader
{
    private const int MaxThumbnailBytes = 4 * 1024 * 1024;

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private bool _managerUnavailable;

    private string _thumbnailTrackKey = string.Empty;
    private byte[]? _thumbnailCache;

    public async Task<MediaSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync();
        if (session is null)
        {
            _thumbnailTrackKey = string.Empty;
            _thumbnailCache = null;
            return MediaSnapshot.Nothing;
        }

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();

            var title = properties?.Title?.Trim() ?? string.Empty;
            var artist = properties?.Artist?.Trim() ?? string.Empty;
            var album = properties?.AlbumTitle?.Trim() ?? string.Empty;
            var source = MediaSourceName.Describe(session.SourceAppUserModelId);

            // Un reproductor abierto sin nada cargado sí tiene sesión pero no tiene pista. Enseñar
            // una carátula vacía con el nombre del programa debajo haría creer que hay algo sonando.
            if (title.Length == 0 && artist.Length == 0)
            {
                return MediaSnapshot.Nothing;
            }

            var trackKey = $"{source}|{title}|{artist}|{album}";
            if (trackKey != _thumbnailTrackKey)
            {
                _thumbnailCache = await TryReadThumbnailAsync(properties?.Thumbnail, cancellationToken);
                _thumbnailTrackKey = trackKey;
            }

            var controls = playback?.Controls;

            return new MediaSnapshot(
                HasSession: true,
                Title: title,
                Artist: artist,
                Album: album,
                SourceApp: source,
                IsPlaying: playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                CanPlayPause: controls?.IsPlayEnabled == true || controls?.IsPauseEnabled == true,
                CanGoNext: controls?.IsNextEnabled == true,
                CanGoPrevious: controls?.IsPreviousEnabled == true,
                Position: MediaProgress.ReadPosition(timeline?.Position),
                Duration: MediaProgress.ReadDuration(timeline?.EndTime - timeline?.StartTime),

                // Diseno D48 - el instante en que esa posicion era cierta. Windows lo entrega en
                // UTC y con valor por defecto cuando el reproductor nunca lo ha informado; ese
                // caso se traduce a null para que nadie extrapole desde el ano uno.
                PositionUpdatedAt: timeline?.LastUpdatedTime is { } updated && updated != default
                    ? updated
                    : null,
                Thumbnail: _thumbnailCache);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            // El reproductor puede cerrarse entre que se obtiene la sesión y se le pregunta. No hay
            // nada que arreglar: en la siguiente lectura habrá otra sesión o ninguna.
            return MediaSnapshot.Nothing;
        }
    }

    public Task<bool> TogglePlayPauseAsync() =>
        CommandAsync(session => session.TryTogglePlayPauseAsync());

    public Task<bool> SkipNextAsync() =>
        CommandAsync(session => session.TrySkipNextAsync());

    public Task<bool> SkipPreviousAsync() =>
        CommandAsync(session => session.TrySkipPreviousAsync());

    private async Task<bool> CommandAsync(
        Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> command)
    {
        var session = await GetSessionAsync();
        if (session is null)
        {
            return false;
        }

        try
        {
            return await command(session);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return false;
        }
    }

    private async Task<GlobalSystemMediaTransportControlsSession?> GetSessionAsync()
    {
        if (_managerUnavailable)
        {
            return null;
        }

        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            return _manager?.GetCurrentSession();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            // En una edición de Windows sin el servicio de medios esto falla siempre igual. Se
            // recuerda para no reintentar dos veces por segundo durante toda la sesión.
            _managerUnavailable = true;
            return null;
        }
    }

    private static async Task<byte[]?> TryReadThumbnailAsync(
        IRandomAccessStreamReference? reference,
        CancellationToken cancellationToken)
    {
        if (reference is null)
        {
            return null;
        }

        try
        {
            using var stream = await reference.OpenReadAsync().AsTask(cancellationToken);
            if (stream.Size == 0 || stream.Size > MaxThumbnailBytes)
            {
                return null;
            }

            var bytes = new byte[stream.Size];
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size).AsTask(cancellationToken);
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return null;
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is COMException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ObjectDisposedException
            or ArgumentException
            or NotSupportedException
            or TypeLoadException
            or FileNotFoundException;
}
