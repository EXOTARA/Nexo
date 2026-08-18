using Nexo.Core.Media;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D46 — el reparto en bandas y el suavizado del anillo de la carátula.
///
/// Nada de esto se puede juzgar mirando un visualizador en movimiento: a treinta fotogramas por
/// segundo, «tiembla» y «va por detrás de la música» son la misma sensación difusa. Aquí cada regla
/// se comprueba con un fotograma concreto.
/// </summary>
public sealed class AudioSpectrumTests
{
    [Fact]
    public void Bands_CoverEqualDistanceInOctaves()
    {
        // Repartir el espectro en partes iguales de hercios mete casi toda la música en las dos
        // primeras barras. En octavas, cada banda abarca la misma proporción.
        var (firstLow, firstHigh) = AudioSpectrum.BandRange(0, 8);
        var (lastLow, lastHigh) = AudioSpectrum.BandRange(7, 8);

        Assert.Equal(AudioSpectrum.MinimumHertz, firstLow, 6);
        Assert.Equal(AudioSpectrum.MaximumHertz, lastHigh, 3);
        Assert.Equal(firstHigh / firstLow, lastHigh / lastLow, 6);
    }

    [Fact]
    public void Bands_AreContiguousAndAscending()
    {
        double? previousHigh = null;
        for (var i = 0; i < 16; i++)
        {
            var (low, high) = AudioSpectrum.BandRange(i, 16);

            Assert.True(high > low);
            if (previousHigh is { } previous)
            {
                Assert.Equal(previous, low, 6);
            }

            previousHigh = high;
        }
    }

    [Fact]
    public void ASingleTone_LightsItsOwnBandAndNotTheFarOnes()
    {
        var spectrum = new AudioSpectrum(bandCount: 12);
        const int sampleRate = 48000;

        // Un bin con energía cerca de 1 kHz, todo lo demás en silencio.
        var magnitudes = new double[512];
        var hertzPerBin = sampleRate / 2.0 / magnitudes.Length;
        magnitudes[(int)(1000 / hertzPerBin)] = 1;

        for (var frame = 0; frame < 30; frame++)
        {
            spectrum.Push(magnitudes, sampleRate);
        }

        var loudest = 0;
        for (var i = 1; i < spectrum.BandCount; i++)
        {
            if (spectrum.Levels[i] > spectrum.Levels[loudest])
            {
                loudest = i;
            }
        }

        var (low, high) = AudioSpectrum.BandRange(loudest, spectrum.BandCount);
        Assert.InRange(1000d, low, high);

        // Las bandas graves no deben encenderse con un tono agudo.
        Assert.True(spectrum.Levels[0] < 0.2);
    }

    [Fact]
    public void TheAttackIsFasterThanTheDecay()
    {
        // Un golpe de batería tiene que aparecer al instante; si se suaviza la subida, el anillo va
        // por detrás de la música y se nota. Bajar, en cambio, se hace despacio: es lo que evita el
        // temblor entre lecturas.
        var spectrum = new AudioSpectrum(bandCount: 4);
        var loud = new double[] { 1, 1, 1, 1 };

        spectrum.Push(loud, 48000);
        var afterOneLoudFrame = spectrum.Levels[0];

        for (var i = 0; i < 40; i++)
        {
            spectrum.Push(loud, 48000);
        }

        var settled = spectrum.Levels[0];
        spectrum.Decay();
        var dropInOneFrame = settled - spectrum.Levels[0];

        Assert.True(afterOneLoudFrame > 0.5, "el primer fotograma fuerte debe saltar más de la mitad");
        Assert.True(dropInOneFrame < afterOneLoudFrame, "la caída debe ser más lenta que el ataque");
    }

    [Fact]
    public void QuietMusicStillFillsTheRing()
    {
        // Una canción grabada bajita movería el anillo tres píxeles para siempre si la referencia
        // fuera fija. Al medirse contra el pico reciente, cualquier música lo llena.
        var spectrum = new AudioSpectrum(bandCount: 4);
        var quiet = new double[] { 0.004, 0.004, 0.004, 0.004 };

        for (var i = 0; i < 60; i++)
        {
            spectrum.Push(quiet, 48000);
        }

        Assert.True(spectrum.Levels[0] > 0.6);
    }

    [Fact]
    public void Silence_BringsTheRingDown()
    {
        // Pausar la música no puede dejar el anillo congelado en la última forma: se leería como
        // que sigue sonando.
        var spectrum = new AudioSpectrum(bandCount: 4);
        for (var i = 0; i < 20; i++)
        {
            spectrum.Push([1, 1, 1, 1], 48000);
        }

        var before = spectrum.Levels[0];
        for (var i = 0; i < 60; i++)
        {
            spectrum.Decay();
        }

        Assert.True(spectrum.Levels[0] < before * 0.2);
    }

    [Fact]
    public void LevelsNeverLeaveZeroToOne()
    {
        var spectrum = new AudioSpectrum(bandCount: 8);

        // Una explosión seguida de silencio: el caso que descuadra una normalización ingenua.
        spectrum.Push([50, 50, 50, 50, 50, 50, 50, 50], 48000);
        spectrum.Push([0.001, 0, 0, 0, 0, 0, 0, 0], 48000);

        Assert.All(spectrum.Levels, level => Assert.InRange(level, 0, 1));
    }

    [Fact]
    public void WithoutSamples_ItJustDecays()
    {
        var spectrum = new AudioSpectrum(bandCount: 4);
        spectrum.Push([1, 1, 1, 1], 48000);
        var before = spectrum.Levels[0];

        spectrum.Push([], 48000);
        spectrum.Push([1, 1], 0);

        Assert.True(spectrum.Levels[0] < before);
        Assert.All(spectrum.Levels, level => Assert.InRange(level, 0, 1));
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        var spectrum = new AudioSpectrum(bandCount: 4);
        spectrum.Push([1, 1, 1, 1], 48000);
        spectrum.Reset();

        Assert.All(spectrum.Levels, level => Assert.Equal(0, level));
    }
}
