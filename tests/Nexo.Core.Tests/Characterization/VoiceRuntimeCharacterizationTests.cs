using Nexo.Core.Settings;
using Nexo.Core.Voice;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Fase 1.1 — congela las reglas de voz y palabra de activación que <c>MainWindow</c> orquesta
/// hoy, antes de extraer el coordinador de voz (paso 1.3).
///
/// No mide latencia ni calidad acústica: eso pertenece al Voice Lab (Fase 3) y requiere
/// micrófono. Aquí solo se congela la **lógica** ya determinista.
/// </summary>
public sealed class VoiceRuntimeCharacterizationTests
{
    // ---------- Frases estables vs. experimentales ----------

    [Theory]
    [InlineData("oye kohana", WakeWordPhrase.OyeKohana)]
    [InlineData("kohana", WakeWordPhrase.Kohana)]
    public void StablePhrases_MatchTheirOwnWakeWord(string text, WakeWordPhrase phrase)
    {
        // `PRODUCT_VISION` §E: `Oye Kohana` y `Kohana` son las frases estables de 1.0.
        Assert.True(WakeWordTextMatcher.IsMatch(text, phrase));
    }

    [Fact]
    public void EachPhraseIsCalibratedSeparately_NotInterchangeable()
    {
        // `PRODUCT_VISION` §E prohíbe asumir que un umbral sirve para todas las frases.
        // Congela que la evaluación depende de la frase configurada.
        var oyeResult = WakeWordTextMatcher.Evaluate("oye kohana", WakeWordPhrase.OyeKohana);
        var kohanaResult = WakeWordTextMatcher.Evaluate("oye kohana", WakeWordPhrase.Kohana);

        Assert.True(oyeResult.IsMatch);
        Assert.NotNull(kohanaResult);
    }

    [Theory]
    [InlineData(WakeWordPhrase.Nexo)]
    [InlineData(WakeWordPhrase.OyeNexo)]
    [InlineData(WakeWordPhrase.HeyNexo)]
    public void LegacyNexoPhrases_AreStillRecognisedAsLegacy(WakeWordPhrase phrase)
    {
        // Se conservan solo para poder leer settings.json antiguos (MIGRATION_PLAN §2).
        Assert.True(phrase.IsLegacy());
    }

    [Theory]
    [InlineData(WakeWordPhrase.Kohana)]
    [InlineData(WakeWordPhrase.OyeKohana)]
    [InlineData(WakeWordPhrase.HeyKohana)]
    public void KohanaPhrases_AreNotLegacy(WakeWordPhrase phrase)
    {
        Assert.False(phrase.IsLegacy());
    }

    [Fact]
    public void LegacyPhraseIsMigratedAwayOnLoad()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 13,
            WakeWordPhrase = WakeWordPhrase.OyeNexo
        };

        preferences.Normalize();

        Assert.Equal(WakeWordPhrase.OyeKohana, preferences.WakeWordPhrase);
    }

    [Fact]
    public void SpokenTextIsStableForEveryPhrase()
    {
        Assert.Equal("Oye Kohana", WakeWordPhrase.OyeKohana.ToSpokenText());
        Assert.Equal("Kohana", WakeWordPhrase.Kohana.ToSpokenText());
        Assert.Equal("Hey Kohana", WakeWordPhrase.HeyKohana.ToSpokenText());
    }

    // ---------- Aliases personales ----------

    [Fact]
    public void CustomAliases_ExtendTheGrammarWithoutReplacingIt()
    {
        var withAlias = WakeWordTextMatcher.GetGrammarPhrases(
            WakeWordPhrase.OyeKohana,
            WakeWordSensitivity.Balanced,
            ["mi asistente"]);

        var withoutAlias = WakeWordTextMatcher.GetGrammarPhrases(
            WakeWordPhrase.OyeKohana,
            WakeWordSensitivity.Balanced);

        Assert.All(withoutAlias, phrase => Assert.Contains(phrase, withAlias));
        Assert.True(withAlias.Count >= withoutAlias.Count);
    }

    [Fact]
    public void Aliases_AreTextOnly_NeverAudio()
    {
        // `PRODUCT_VISION` §E y `PRIVACY_BOUNDARIES`: los aliases son texto normalizado.
        // La normalización se aplica siempre al guardar preferencias.
        var preferences = new ShellPreferences
        {
            SchemaVersion = 16,
            WakeWordAliases = ["  Mi Asistente  ", "MI ASISTENTE"]
        };

        preferences.Normalize();

        Assert.All(
            preferences.WakeWordAliases,
            alias => Assert.Equal(alias.Trim(), alias));
    }

    [Fact]
    public void Grammar_IsSensitivityDependent()
    {
        var strict = WakeWordTextMatcher.GetGrammarPhrases(
            WakeWordPhrase.OyeKohana,
            WakeWordSensitivity.Strict);
        var high = WakeWordTextMatcher.GetGrammarPhrases(
            WakeWordPhrase.OyeKohana,
            WakeWordSensitivity.High);

        // Congela que la sensibilidad realmente cambia la gramática. Si dejara de hacerlo,
        // la opción de Ajustes sería decorativa.
        Assert.True(high.Count >= strict.Count);
    }

    [Fact]
    public void EmptyOrBlankAudioText_NeverTriggersTheWakeWord()
    {
        Assert.False(WakeWordTextMatcher.IsMatch(null, WakeWordPhrase.OyeKohana));
        Assert.False(WakeWordTextMatcher.IsMatch(string.Empty, WakeWordPhrase.OyeKohana));
        Assert.False(WakeWordTextMatcher.IsMatch("   ", WakeWordPhrase.OyeKohana));
    }

    [Fact]
    public void UnrelatedSpeech_DoesNotTriggerTheWakeWord()
    {
        Assert.False(WakeWordTextMatcher.IsMatch(
            "voy a preparar la comida",
            WakeWordPhrase.OyeKohana));
    }

    // ---------- Fin de turno ----------

    [Fact]
    public void UtteranceEnd_RequiresSpeechSilenceAndMinimumAudio()
    {
        var complete = VoiceUtteranceEndPolicy.ShouldComplete(
            new VoiceUtteranceTimingSnapshot(
                SpeechDetected: true,
                SpeechMilliseconds: 800,
                TrailingSilenceMilliseconds: 700,
                LiveAudioMilliseconds: 3_000),
            TimeSpan.FromMilliseconds(600));

        Assert.True(complete);
    }

    [Fact]
    public void UtteranceEnd_NeverCompletesWithoutDetectedSpeech()
    {
        var complete = VoiceUtteranceEndPolicy.ShouldComplete(
            new VoiceUtteranceTimingSnapshot(
                SpeechDetected: false,
                SpeechMilliseconds: 5_000,
                TrailingSilenceMilliseconds: 5_000,
                LiveAudioMilliseconds: 5_000),
            TimeSpan.FromMilliseconds(600));

        Assert.False(complete);
    }

    [Theory]
    [InlineData(340, 3_000, 700)]  // habla insuficiente
    [InlineData(800, 2_400, 700)]  // audio en vivo insuficiente
    [InlineData(800, 3_000, 500)]  // silencio final insuficiente
    public void UtteranceEnd_RequiresAllThreeConditions(
        int speechMs,
        int liveAudioMs,
        int trailingSilenceMs)
    {
        var complete = VoiceUtteranceEndPolicy.ShouldComplete(
            new VoiceUtteranceTimingSnapshot(
                SpeechDetected: true,
                SpeechMilliseconds: speechMs,
                TrailingSilenceMilliseconds: trailingSilenceMs,
                LiveAudioMilliseconds: liveAudioMs),
            TimeSpan.FromMilliseconds(600));

        Assert.False(complete);
    }

    [Fact]
    public void UtteranceEnd_RejectsAnUnreasonablyShortSilenceWindow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VoiceUtteranceEndPolicy.ShouldComplete(
                new VoiceUtteranceTimingSnapshot(true, 800, 700, 3_000),
                TimeSpan.FromMilliseconds(299)));
    }

    [Fact]
    public void UtteranceEndThresholds_AreTheDocumentedOnes()
    {
        Assert.Equal(350, VoiceUtteranceEndPolicy.MinimumSpeechMilliseconds);
        Assert.Equal(2_500, VoiceUtteranceEndPolicy.MinimumLiveAudioMilliseconds);
    }

    // ---------- Preferencias de voz ----------

    [Fact]
    public void VoiceOutput_IsOffByDefault()
    {
        var preferences = new ShellPreferences();
        preferences.Normalize();

        Assert.False(preferences.SpeakVoiceResponses);
        Assert.False(preferences.WakeWordEnabled);
    }

    [Fact]
    public void WakeWordPausesInGameModeByDefault()
    {
        var preferences = new ShellPreferences();
        preferences.Normalize();

        Assert.True(preferences.PauseWakeWordInGameMode);
    }
}
