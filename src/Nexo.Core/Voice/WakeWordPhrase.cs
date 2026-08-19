namespace Nexo.Core.Voice;

public enum WakeWordPhrase
{
    // Valores heredados. Se conservan para poder leer settings.json antiguos.
    Nexo = 0,
    OyeNexo = 1,
    HeyNexo = 2,

    // Identidad Kohana. Se conservan porque siguen siendo elegibles y porque hay settings.json
    // con estos valores; dejaron de ser la opción por omisión en el Diseño D69.
    Kohana = 3,
    OyeKohana = 4,
    HeyKohana = 5,

    // Diseño D69 — identidad Sakura.
    //
    // El cambio de nombre no es de gusto: "kohana" no existe en el léxico español del reconocedor
    // y por eso no podía salir escrita nunca. Medido con la voz de Windows, "oye sakura" se
    // transcribe perfecto las tres veces, y "oye kohana" sale como "oye co ana" o "oye cubana".
    Sakura = 6,
    OyeSakura = 7,
    HeySakura = 8
}

public static class WakeWordPhraseExtensions
{
    public static string ToSpokenText(this WakeWordPhrase phrase) => phrase switch
    {
        WakeWordPhrase.OyeSakura => "Oye Sakura",
        WakeWordPhrase.HeySakura => "Hey Sakura",
        WakeWordPhrase.Sakura => "Sakura",
        WakeWordPhrase.OyeKohana => "Oye Kohana",
        WakeWordPhrase.HeyKohana => "Hey Kohana",
        WakeWordPhrase.Kohana => "Kohana",
        WakeWordPhrase.OyeNexo => "Oye Nexo",
        WakeWordPhrase.HeyNexo => "Hey Nexo",
        _ => "Oye Sakura"
    };

    public static bool IsLegacy(this WakeWordPhrase phrase) => phrase is
        WakeWordPhrase.Nexo or
        WakeWordPhrase.OyeNexo or
        WakeWordPhrase.HeyNexo;

    /// <summary>
    /// Diseño D69 — a qué nombre se refiere la frase. Lo que cambia entre una familia y otra es la
    /// palabra que hay que reconocer, no la forma de reconocerla.
    /// </summary>
    public static bool IsSakura(this WakeWordPhrase phrase) => phrase is
        WakeWordPhrase.Sakura or
        WakeWordPhrase.OyeSakura or
        WakeWordPhrase.HeySakura;
}
