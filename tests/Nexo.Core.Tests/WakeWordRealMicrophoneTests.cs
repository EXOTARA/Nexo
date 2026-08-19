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


    // ---------- Segunda grabación: a dos metros y con un video sonando ----------
    //
    // Diseño D68 — catorce intentos. Aquí Vosk deja de oír algo parecido a "kohana": escribe
    // "cógeme", "con gana", "coge ama", "ojalá". Whisper, sobre el mismo audio, escribe catorce
    // veces "Oye, coca". La señal que llega está degradada, no es solo cuestión de léxico.
    private static readonly string[] FarFieldAttempts =
    [
        "gana",
        "oye cógeme",
        "oye cógeme",
        "oye cojan",
        "oye ojalá",
        "oye con jarana",
        "fluye con ganas",
        "oye cogerme",
        "oye coge ama",
        "oye con gana",
        "oye con gana"
    ];

    // ---------- Tercera grabación: hablar seguido sin decir nunca la frase ----------
    //
    // Adler la grabó a propósito con trampas: koala, Kyoto, kilovatios, Kazajistán, criptoniano,
    // kimono. Es el vecindario fonético de "Kohana" y es la mitad que de verdad importa: subir la
    // tolerancia no vale nada si con esto Kohana se despierta sola.
    private static readonly string[] TheDecoyRecording =
    [
        "el cilantro fresco baila alegremente sobre un ala ancha submarina mientras un reloj de arena cuenta los minutos que faltan para que los gatos aprendan a volar en marte",
        "el café el café frío sabe a metal antiguo cuando llueve de noche",
        "con zapato morado camina siempre por la banqueta las nueve de cartón flotan sobre el mar de gelatina",
        "el número cinco o de los martes nublados las tostada siempre candela donde hay risas",
        "concordia deck cara teca comía que fir con ketchup en una kayak mientras un koala colosal toca el chico y miraban kimono morado en kioto",
        "de repente bianco a la colosal que toca el qué cosa brooks creek",
        "cala en yo es el cual a que era un krypton y no aficionada al quedando dejaba se kioto quería matron récord mundial de kilo ates consumidor velando que un kamikaze todo quedó registrado en un video de un kilo hay",
        "kazajistán kioto kodak co koala kilo que lo bajito que lo que fir ketchup"
    ];

    [Fact]
    public void AtTwoMetresWithNoise_TheWakeWordIsStillMostlyLost()
    {
        var woken = FarFieldAttempts.Count(
            attempt => WakeWordTextMatcher.IsMatch(attempt, WakeWordPhrase.OyeKohana, WakeWordSensitivity.Balanced));

        // Seis de catorce intentos, y era uno antes del D68. La cifra está escrita aquí sin adornos
        // porque es el argumento para cambiar de reconocedor en la Fase 3: ninguna lista de
        // variantes arregla que a dos metros la palabra deje de sonar como es.
        Assert.Equal(6, woken);
    }

    [Fact]
    public void TheDecoyRecordingNeverWakesKohana()
    {
        foreach (var sensitivity in new[]
                 {
                     WakeWordSensitivity.Strict, WakeWordSensitivity.Balanced, WakeWordSensitivity.High
                 })
        {
            foreach (var line in TheDecoyRecording)
            {
                Assert.False(
                    WakeWordTextMatcher.IsMatch(line, WakeWordPhrase.OyeKohana, sensitivity),
                    $"Un despertar falso en sensibilidad {sensitivity}: “{line}”.");
            }
        }
    }

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
