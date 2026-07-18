namespace Nexo.Core.Voice;

public enum WakeWordPhrase
{
    Nexo,
    OyeNexo
}

public static class WakeWordPhraseExtensions
{
    public static string ToSpokenText(this WakeWordPhrase phrase) => phrase switch
    {
        WakeWordPhrase.OyeNexo => "Oye Nexo",
        _ => "Nexo"
    };
}
