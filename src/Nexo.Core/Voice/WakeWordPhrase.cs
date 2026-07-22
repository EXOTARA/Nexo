namespace Nexo.Core.Voice;

public enum WakeWordPhrase
{
    // Valores heredados. Se conservan para poder leer settings.json antiguos.
    Nexo = 0,
    OyeNexo = 1,
    HeyNexo = 2,

    // Identidad Kohana.
    Kohana = 3,
    OyeKohana = 4,
    HeyKohana = 5
}

public static class WakeWordPhraseExtensions
{
    public static string ToSpokenText(this WakeWordPhrase phrase) => phrase switch
    {
        WakeWordPhrase.OyeKohana => "Oye Kohana",
        WakeWordPhrase.HeyKohana => "Hey Kohana",
        WakeWordPhrase.Kohana => "Kohana",
        WakeWordPhrase.OyeNexo => "Oye Nexo",
        WakeWordPhrase.HeyNexo => "Hey Nexo",
        _ => "Oye Kohana"
    };

    public static bool IsLegacy(this WakeWordPhrase phrase) => phrase is
        WakeWordPhrase.Nexo or
        WakeWordPhrase.OyeNexo or
        WakeWordPhrase.HeyNexo;
}
