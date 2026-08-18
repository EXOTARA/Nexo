using Nexo.Core.Media;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D48 — la posición real de la pista, contada desde la última que informó el reproductor.
///
/// Estas pruebas existen por un defecto que se vio usando la aplicación: la barra se quedaba
/// clavada entre cinco y siete segundos y luego pegaba un salto. No era un fallo de dibujo —
/// Windows no actualiza la posición continuamente, la refresca cuando al reproductor le parece.
/// </summary>
public sealed class MediaPositionResolveTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WhilePlaying_TimeKeepsRunningBetweenReports()
    {
        // Spotify dijo «voy por 1:00» hace cuatro segundos. Ahora va por 1:04, aunque no lo haya
        // vuelto a decir. Esto es el defecto entero.
        var resolved = MediaProgress.Resolve(
            position: TimeSpan.FromMinutes(1),
            lastUpdatedAt: Now.AddSeconds(-4),
            now: Now,
            isPlaying: true,
            duration: TimeSpan.FromMinutes(4));

        Assert.Equal(TimeSpan.FromSeconds(64), resolved);
    }

    [Fact]
    public void Paused_TheTimeStandsStill()
    {
        // En pausa la posición no avanza. Contar el reloj aquí haría correr la barra con la música
        // parada, que es peor que el defecto original.
        var resolved = MediaProgress.Resolve(
            position: TimeSpan.FromMinutes(1),
            lastUpdatedAt: Now.AddSeconds(-30),
            now: Now,
            isPlaying: false,
            duration: TimeSpan.FromMinutes(4));

        Assert.Equal(TimeSpan.FromMinutes(1), resolved);
    }

    [Fact]
    public void WithoutAnAnchor_NothingIsInvented()
    {
        // Un reproductor que nunca informó cuándo era cierta su posición no da derecho a contar
        // tiempo sobre ella.
        var resolved = MediaProgress.Resolve(
            position: TimeSpan.FromMinutes(1),
            lastUpdatedAt: null,
            now: Now,
            isPlaying: true,
            duration: TimeSpan.FromMinutes(4));

        Assert.Equal(TimeSpan.FromMinutes(1), resolved);
    }

    [Fact]
    public void AfterALongSilence_ItStopsGuessing()
    {
        // El equipo se suspendió, o el reproductor dejó de informar. Seguir sumando contaría una
        // mentira cada vez mayor; quedarse quieto hasta la siguiente lectura, no.
        var resolved = MediaProgress.Resolve(
            position: TimeSpan.FromMinutes(1),
            lastUpdatedAt: Now - MediaProgress.MaximumExtrapolation - TimeSpan.FromSeconds(1),
            now: Now,
            isPlaying: true,
            duration: TimeSpan.FromMinutes(4));

        Assert.Equal(TimeSpan.FromMinutes(1), resolved);
    }

    [Fact]
    public void ItNeverRunsPastTheEndOfTheTrack()
    {
        // Al terminar la canción, el reproductor tarda en anunciar la siguiente. Sin tope, la barra
        // se saldría de su carril durante esos segundos.
        var resolved = MediaProgress.Resolve(
            position: TimeSpan.FromSeconds(238),
            lastUpdatedAt: Now.AddSeconds(-10),
            now: Now,
            isPlaying: true,
            duration: TimeSpan.FromSeconds(240));

        Assert.Equal(TimeSpan.FromSeconds(240), resolved);
    }

    [Fact]
    public void AClockThatDisagrees_DoesNotPushTheBarBackwards()
    {
        // El instante lo pone el servicio de medios y la hora la pone Kohana; no tienen por qué
        // coincidir al milisegundo. Un tiempo negativo no significa que la pista haya retrocedido.
        var resolved = MediaProgress.Resolve(
            position: TimeSpan.FromMinutes(1),
            lastUpdatedAt: Now.AddSeconds(3),
            now: Now,
            isPlaying: true,
            duration: TimeSpan.FromMinutes(4));

        Assert.Equal(TimeSpan.FromMinutes(1), resolved);
    }

    [Fact]
    public void WithoutAPosition_ThereIsNothingToResolve() =>
        Assert.Null(MediaProgress.Resolve(null, Now, Now, isPlaying: true, duration: null));

    [Fact]
    public void ALiveStreamKeepsCountingWithoutAnEnd()
    {
        // Una emisión en directo no tiene duración, pero el tiempo transcurrido sí avanza.
        var resolved = MediaProgress.Resolve(
            position: TimeSpan.FromMinutes(3),
            lastUpdatedAt: Now.AddSeconds(-5),
            now: Now,
            isPlaying: true,
            duration: null);

        Assert.Equal(TimeSpan.FromSeconds(185), resolved);
    }
}
