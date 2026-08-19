using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D66 — lo que Vosk escribió de verdad cuando Adler dijo "Oye Kohana" ocho veces.
///
/// Las demás pruebas de <see cref="WakeWordTextMatcher"/> usan frases inventadas por quien escribió
/// el código, que es exactamente el problema: uno inventa las variantes que se le ocurren, y el
/// reconocedor produce otras. Estas dieciséis cadenas salen de una grabación del micrófono de Adler
/// (1:43, ocho intentos), pasadas por el mismo modelo Vosk que lleva Kohana instalado.
///
/// El resultado antes de tocar nada: **cero de ocho** en sensibilidad Equilibrada, que es la que
/// viene puesta. La palabra de activación no funcionaba, y ninguna prueba lo decía porque ninguna
/// prueba había oído hablar a nadie.
///
/// Sigue habiendo dos intentos que no despiertan, y están aquí como recordatorio de que esto es un
/// parche sobre un techo conocido: Vosk no tiene "kohana" en su léxico español y no hay lista de
/// variantes que arregle eso del todo. La salida de verdad es la Fase 3 del roadmap.
/// </summary>
public sealed class WakeWordRealMicrophoneTests
{
    // Los ocho intentos, tal como los escribió Vosk. El resto de la grabación (las órdenes) no
    // debe despertar nada y se comprueba aparte.
    private static readonly string[] WakeAttempts =
    [
        "oye jana",
        "rico jena",
        "oye eco jana",
        "coja man",
        "oye cojan",
        "hoy eco gana",
        "oye cogerla",
        "hoy ico jana"
    ];

    private static readonly string[] EverythingElseInTheRecording =
    [
        "bueno expresión escucha",
        "dame una receta de un pay de limón",
        "dime en los acontecimientos más importantes de la guerra y los pasteles en méxico",
        "porque en méxico tiene tan mala reputación",
        "qué fue lo que salvó al salvador de la violencia y la corrupción",
        "abre la calculadora",
        "abre un poco el",
        "habrá un pobre shell"
    ];

    [Fact]
    public void AtTheDefaultSensitivity_MostOfTheRealAttemptsWakeKohana()
    {
        var woken = WakeAttempts.Count(
            attempt => WakeWordTextMatcher.IsMatch(attempt, WakeWordPhrase.OyeKohana, WakeWordSensitivity.Balanced));

        // Cinco de ocho. Era cero antes del D66, y subirlo de aquí exige cambiar de reconocedor,
        // no añadir más variantes: las dos que faltan son "rico jena" —Vosk oyó "rico" por "oye"—
        // y "oye cogerla", que no se acepta a propósito porque "cogerla" es una palabra real y un
        // despertar falso ahí sería peor que el intento perdido.
        Assert.Equal(5, woken);
    }

    [Fact]
    public void AtHighSensitivity_OneMoreGetsThrough()
    {
        var woken = WakeAttempts.Count(
            attempt => WakeWordTextMatcher.IsMatch(attempt, WakeWordPhrase.OyeKohana, WakeWordSensitivity.High));

        Assert.Equal(6, woken);
    }

    [Fact]
    public void NothingElseSaidInTheRecordingWakesKohana()
    {
        // Las órdenes y la charla no pueden despertar nada, en ninguna sensibilidad. Es la mitad
        // que importa de subir la tolerancia: si esto falla, el arreglo no vale.
        foreach (var sensitivity in new[]
                 {
                     WakeWordSensitivity.Strict, WakeWordSensitivity.Balanced, WakeWordSensitivity.High
                 })
        {
            foreach (var phrase in EverythingElseInTheRecording)
            {
                Assert.False(
                    WakeWordTextMatcher.IsMatch(phrase, WakeWordPhrase.OyeKohana, sensitivity),
                    $"“{phrase}” despertó a Kohana en sensibilidad {sensitivity}.");
            }
        }
    }

    [Fact]
    public void TheStrictSensitivityStillDemandsTheWordWrittenAsKohana()
    {
        // Precisa no se toca: es la que existe para quien prefiera no despertar nunca por error.
        foreach (var attempt in WakeAttempts)
        {
            Assert.False(
                WakeWordTextMatcher.IsMatch(attempt, WakeWordPhrase.OyeKohana, WakeWordSensitivity.Strict));
        }

        Assert.True(
            WakeWordTextMatcher.IsMatch("oye kohana", WakeWordPhrase.OyeKohana, WakeWordSensitivity.Strict));
    }
}
