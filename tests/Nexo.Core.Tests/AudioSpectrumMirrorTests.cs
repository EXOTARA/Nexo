using Nexo.Core.Media;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D54 — el espejo del anillo.
///
/// Nace de un defecto que se vio usándolo: «el movimiento con el sonido casi siempre es del mismo
/// lado». No era un fallo de dibujo. Los rayos llevaban las bandas en orden alrededor del círculo,
/// así que los graves —donde vive casi toda la energía de la música— caían siempre en el mismo
/// arco y el resto del anillo se quedaba quieto.
/// </summary>
public sealed class AudioSpectrumMirrorTests
{
    [Fact]
    public void TheTwoHalvesShowTheSameBand()
    {
        // Es lo que hace que el anillo respire entero: cada banda aparece dos veces, una a cada
        // lado, así que ningún costado puede moverse solo.
        const int rays = 128;
        const int bands = 64;

        for (var ray = 1; ray < rays / 2; ray++)
        {
            Assert.Equal(
                AudioSpectrum.MirroredBand(ray, rays, bands),
                AudioSpectrum.MirroredBand(rays - ray, rays, bands));
        }
    }

    [Fact]
    public void TheBassSitsAtTheTopAndTheTrebleAtTheBottom()
    {
        const int rays = 128;
        const int bands = 64;

        // Las doce en punto es donde arranca el recorrido: la banda más grave.
        Assert.Equal(0, AudioSpectrum.MirroredBand(0, rays, bands));

        // Las seis, el punto más lejano en las dos direcciones: la más aguda.
        Assert.Equal(bands - 1, AudioSpectrum.MirroredBand(rays / 2, rays, bands));
    }

    [Fact]
    public void ItWalksTheSpectrumWithoutJumping()
    {
        // De un rayo al siguiente la banda avanza como mucho uno. Un salto mayor dejaría huecos en
        // el recorrido y el anillo enseñaría el espectro a trozos.
        const int rays = 128;
        const int bands = 64;

        for (var ray = 1; ray <= rays / 2; ray++)
        {
            var step = AudioSpectrum.MirroredBand(ray, rays, bands)
                - AudioSpectrum.MirroredBand(ray - 1, rays, bands);

            Assert.InRange(step, 0, 1);
        }
    }

    [Fact]
    public void EveryRayLandsOnARealBand()
    {
        const int rays = 130;
        const int bands = 64;

        for (var ray = 0; ray < rays; ray++)
        {
            Assert.InRange(AudioSpectrum.MirroredBand(ray, rays, bands), 0, bands - 1);
        }
    }

    [Fact]
    public void ItWorksWhenThereAreMoreBandsThanRays()
    {
        // No es la configuración que usa Kohana, pero nada impide pedirla y no puede reventar.
        Assert.InRange(AudioSpectrum.MirroredBand(3, 8, 64), 0, 63);
    }

    [Fact]
    public void ImpossibleShapesAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioSpectrum.MirroredBand(-1, 128, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioSpectrum.MirroredBand(0, 1, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioSpectrum.MirroredBand(0, 128, 0));
    }

    [Fact]
    public void ThePerceptualCurveLiftsTheMiddleWithoutTouchingTheEnds()
    {
        // La otra mitad del defecto: «y muy poco». Dividir por el pico deja casi todas las bandas
        // pegadas al suelo, porque en una mezcla real una sola se lleva la mayor parte de la
        // energía. La curva sube esa zona media; el silencio y el máximo se quedan donde estaban.
        var spectrum = new AudioSpectrum(bandCount: 2);

        // Una banda al máximo y otra a la décima parte, sostenidas.
        for (var i = 0; i < 40; i++)
        {
            spectrum.Push([1.0, 0.1], 48000);
        }

        Assert.True(spectrum.Levels[0] > 0.95, "la banda fuerte debe llegar arriba");

        // Sin curva, una décima parte de la energía daría ~0.1 y el rayo no se vería moverse.
        Assert.True(
            spectrum.Levels[1] > 0.35,
            $"la banda media debe subir de forma visible, y quedó en {spectrum.Levels[1]:0.00}");
    }
}
