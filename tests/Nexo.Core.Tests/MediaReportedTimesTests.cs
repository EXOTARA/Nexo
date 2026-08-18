using Nexo.Core.Media;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D57 — cómo se lee lo que informa el reproductor antes de que nadie cuente el tiempo.
///
/// Estas pruebas existen por un defecto que se vio usando la aplicación y que sobrevivió a D48:
/// la barra y el cronómetro seguían clavados durante los primeros segundos de **cada** pista.
/// D48 arregló el tramo largo —contar el tiempo entre lectura y lectura— pero el principio de la
/// pista no llegaba a entrar en ese cálculo, porque la posición cero se descartaba antes como si
/// fuera desconocida y sin posición conocida no hay desde dónde contar.
///
/// El cero es la única frontera donde posición y duración tienen reglas contrarias, y compartir
/// una sola regla para las dos es lo que rompía la posición.
/// </summary>
public sealed class MediaReportedTimesTests
{
    [Fact]
    public void PositionZero_IsTheStartOfTheTrack_NotAnUnknown()
    {
        // El defecto entero: aquí devolvía null y el cronómetro no arrancaba hasta que Spotify
        // informaba su primera posición de verdad, cinco o seis segundos después.
        Assert.Equal(TimeSpan.Zero, MediaProgress.ReadPosition(TimeSpan.Zero));
    }

    [Fact]
    public void NegativePosition_IsUnknown()
    {
        // Un reproductor que aún no ha arrancado informa tiempos negativos.
        Assert.Null(MediaProgress.ReadPosition(TimeSpan.FromSeconds(-3)));
    }

    [Fact]
    public void MissingPosition_StaysUnknown()
    {
        Assert.Null(MediaProgress.ReadPosition(null));
    }

    [Fact]
    public void RealPosition_PassesThroughUntouched()
    {
        Assert.Equal(TimeSpan.FromSeconds(65), MediaProgress.ReadPosition(TimeSpan.FromSeconds(65)));
    }

    [Fact]
    public void DurationZero_MeansNoEnding_NotAZeroLengthTrack()
    {
        // La regla contraria a la de la posición: una emisión en directo informa duración cero y
        // enseñar «0:00» a la derecha haría pensar que la duración no se pudo leer.
        Assert.Null(MediaProgress.ReadDuration(TimeSpan.Zero));
    }

    [Fact]
    public void RealDuration_PassesThroughUntouched()
    {
        Assert.Equal(TimeSpan.FromMinutes(4), MediaProgress.ReadDuration(TimeSpan.FromMinutes(4)));
    }

    [Fact]
    public void FromZero_TheClockStartsCountingImmediately()
    {
        // Lo que el defecto impedía, extremo a extremo: pista recién empezada, el reproductor dice
        // «voy por 0:00» y no vuelve a decir nada en tres segundos. El cronómetro tiene que ir por
        // 0:03, no seguir clavado en 0:00.
        var now = new DateTimeOffset(2026, 8, 18, 12, 0, 3, TimeSpan.Zero);
        var reportedAt = now.AddSeconds(-3);

        var resolved = MediaProgress.Resolve(
            MediaProgress.ReadPosition(TimeSpan.Zero),
            reportedAt,
            now,
            isPlaying: true,
            duration: TimeSpan.FromMinutes(4));

        Assert.Equal(TimeSpan.FromSeconds(3), resolved);
    }
}
