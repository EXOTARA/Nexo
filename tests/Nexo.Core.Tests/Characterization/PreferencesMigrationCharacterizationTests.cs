using Nexo.Core.Ai;
using Nexo.Core.Settings;
using Nexo.Core.Voice;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Fase 1.1 — congela las migraciones de preferencias (esquema v19) y los saneamientos que
/// <see cref="ShellPreferences.Normalize"/> aplica en cada carga.
///
/// Regla de `MIGRATION_PLAN.md`: cada incremento es aditivo, con default seguro, y **nunca**
/// borra datos del usuario.
///
/// La versión esperada sube a la par que el esquema real (v17 → v18 en Diseño D6 y v18 → v19 en Diseño D9):
/// esta prueba existe para detectar un cambio de esquema NO intencionado, no para congelar un
/// número para siempre.
/// </summary>
public sealed class PreferencesMigrationCharacterizationTests
{
    private static readonly int CurrentSchemaVersion = ShellPreferences.CurrentSchemaVersion;

    [Fact]
    public void CurrentSchemaVersion_IsNineteen()
    {
        var preferences = new ShellPreferences();
        preferences.Normalize();

        Assert.Equal(CurrentSchemaVersion, preferences.SchemaVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(13)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(18)]
    public void AnyOlderSchema_MigratesForwardToTheCurrentVersion(int startingVersion)
    {
        var preferences = new ShellPreferences { SchemaVersion = startingVersion };

        preferences.Normalize();

        Assert.Equal(CurrentSchemaVersion, preferences.SchemaVersion);
    }

    [Fact]
    public void MigratingToEighteen_NeverErasesFlowListsAlreadyPresent()
    {
        // Misma garantía que el escalón v16 con los aliases de palabra de activación: una
        // actualización añade valores por omisión, nunca borra lo que el usuario ya tenía.
        var preferences = new ShellPreferences
        {
            SchemaVersion = 17,
            FlowDictionary = ["cojana=Sakura"],
            FlowSnippets = ["mi correo=adler@ejemplo.com"]
        };

        preferences.Normalize();

        Assert.Equal(["cojana=Sakura"], preferences.FlowDictionary);
        Assert.Equal(["mi correo=adler@ejemplo.com"], preferences.FlowSnippets);
    }

    [Fact]
    public void OldFileWithoutFlowLists_GetsEmptyListsInsteadOfNull()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 10,
            FlowDictionary = null!,
            FlowSnippets = null!
        };

        preferences.Normalize();

        Assert.Empty(preferences.FlowDictionary);
        Assert.Empty(preferences.FlowSnippets);
    }

    [Fact]
    public void MigratingToNineteen_LeavesMemoryOff_BecauseAnUpdateIsNotConsent()
    {
        // El roadmap dice que la memoria "nunca" se activa por defecto. Actualizar Sakura no es
        // que la persona haya dicho que sí.
        var preferences = new ShellPreferences { SchemaVersion = 18 };

        preferences.Normalize();

        Assert.False(preferences.Memory.Enabled);
        Assert.False(preferences.Memory.RememberPreferences);
        Assert.False(preferences.Memory.RememberConversation);
        Assert.False(preferences.Memory.RememberHabits);
    }

    [Fact]
    public void NormalizeIsIdempotent()
    {
        var preferences = new ShellPreferences { SchemaVersion = 0 };

        preferences.Normalize();
        var afterFirst = Snapshot(preferences);
        preferences.Normalize();
        var afterSecond = Snapshot(preferences);

        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public void FreshInstall_IsPrivateAndQuietByDefault()
    {
        var preferences = new ShellPreferences();
        preferences.Normalize();

        // `PRODUCT_VISION` §G/§J y `PRIVACY_BOUNDARIES`: nada invasivo activado de fábrica.
        Assert.False(preferences.SaveConversationHistory);
        Assert.False(preferences.WakeWordEnabled);
        Assert.False(preferences.SpeakVoiceResponses);
        Assert.False(preferences.ShareSystemMetricsWithAi);
        Assert.False(preferences.StartWithWindows);
        Assert.False(preferences.HasCompletedOnboarding);
        Assert.Equal(AiProviderKind.Disabled, preferences.AiProvider);
    }

    [Fact]
    public void FreshInstall_KeepsProtectiveDefaultsOn()
    {
        var preferences = new ShellPreferences();
        preferences.Normalize();

        Assert.True(preferences.ResourceGovernorEnabled);
        Assert.True(preferences.PauseWakeWordInGameMode);
        Assert.True(preferences.ProtectVisionWhenBusy);
        Assert.True(preferences.MinimizeToTray);
    }

    [Fact]
    public void Migration16_PreservesExistingWakeWordAliases()
    {
        // Corrección registrada en CHANGELOG 0.9.5-beta-hotfix.1: la migración al esquema 16
        // conserva y normaliza los aliases en lugar de borrarlos.
        var preferences = new ShellPreferences
        {
            SchemaVersion = 15,
            WakeWordAliases = ["Kojana", "  cojana  "]
        };

        preferences.Normalize();

        Assert.Equal(CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.NotEmpty(preferences.WakeWordAliases);
        Assert.All(preferences.WakeWordAliases, alias => Assert.Equal(alias.Trim(), alias));
    }

    [Fact]
    public void Migration14_ReplacesTheLegacyAccentColour()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 13,
            AccentColor = "#8B6CFF"
        };

        preferences.Normalize();

        Assert.Equal("#E98AAF", preferences.AccentColor);
    }

    [Fact]
    public void Migration14_KeepsACustomAccentColourTheUserChose()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 13,
            AccentColor = "#123456"
        };

        preferences.Normalize();

        Assert.Equal("#123456", preferences.AccentColor);
    }

    [Fact]
    public void BlankAccentColour_FallsBackToTheSakuraDefault()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = CurrentSchemaVersion,
            AccentColor = "   "
        };

        preferences.Normalize();

        Assert.Equal("#E98AAF", preferences.AccentColor);
    }

    // Diseño D58 — cambiada a propósito: el suelo del rango bajó de 680 a 460 para que Sakura
    // pueda ponerse tan estrecha como una barra lateral. El techo y la forma de recortar no se
    // tocan, y 700 sigue pasando intacto porque sigue estando dentro del rango.
    [Theory]
    [InlineData(100, 460)]
    [InlineData(700, 700)]
    [InlineData(5000, 820)]
    public void Width_IsClampedToTheShellRange(double input, double expected)
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = CurrentSchemaVersion,
            Width = input
        };

        preferences.Normalize();

        Assert.Equal(expected, preferences.Width);
    }

    /// <summary>
    /// Diseño D62 — el suelo cambió de 0.82 a 0.35 y esta caracterización se actualiza a
    /// conciencia. Lo que protegía el 0.82 era la lectura de un texto sobre el escritorio sin
    /// desenfocar, que es lo único que había debajo de Sakura; con el acrílico del sistema detrás,
    /// lo que se ve a través ya viene desenfocado. Donde no hay fondo del sistema la superficie se
    /// pinta opaca pase lo que pase, así que el suelo nuevo no llega a aplicarse ahí.
    /// </summary>
    [Theory]
    [InlineData(0.1, 0.35)]
    [InlineData(0.9, 0.9)]
    [InlineData(3.0, 1.0)]
    public void Opacity_IsClamped(double input, double expected)
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = CurrentSchemaVersion,
            Opacity = input
        };

        preferences.Normalize();

        Assert.Equal(expected, preferences.Opacity);
    }

    [Fact]
    public void ConversationLimit_CollapsesToEightWhenHistoryIsOff()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = CurrentSchemaVersion,
            SaveConversationHistory = false,
            RecentConversationMessageLimit = 30
        };

        preferences.Normalize();

        Assert.Equal(8, preferences.RecentConversationMessageLimit);
    }

    [Fact]
    public void ConversationLimit_IsClampedWhenHistoryIsOn()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = CurrentSchemaVersion,
            SaveConversationHistory = true,
            RecentConversationMessageLimit = 900
        };

        preferences.Normalize();

        Assert.Equal(30, preferences.RecentConversationMessageLimit);
    }

    [Fact]
    public void OutOfRangeEnums_FallBackToSafeValues()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = CurrentSchemaVersion,
            WakeWordPhrase = (WakeWordPhrase)999,
            WakeWordSensitivity = (WakeWordSensitivity)999,
            AiProvider = (AiProviderKind)999
        };

        preferences.Normalize();

        // Diseño D69 — cambiada a conciencia con el cambio de nombre. La frase por omisión es
        // ahora "Oye Sakura": "kohana" no existe en el léxico español del reconocedor y salía
        // escrita "oye co ana", así que conservarla habría sido conservar la única que no
        // funcionaba. La forma —corta, "oye" o "hey"— sí se respeta al migrar.
        Assert.Equal(WakeWordPhrase.OyeSakura, preferences.WakeWordPhrase);
        Assert.Equal(WakeWordSensitivity.Balanced, preferences.WakeWordSensitivity);
        Assert.Equal(AiProviderKind.Disabled, preferences.AiProvider);
    }

    [Fact]
    public void VoiceInputDeviceNumber_NeverGoesBelowMinusOne()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = CurrentSchemaVersion,
            VoiceInputDeviceNumber = -50
        };

        preferences.Normalize();

        Assert.Equal(-1, preferences.VoiceInputDeviceNumber);
    }

    [Fact]
    public void MigrationNeverDowngradesTheSchema()
    {
        // Un archivo de una versión futura no debe retroceder: Normalize solo avanza.
        var preferences = new ShellPreferences { SchemaVersion = 99 };

        preferences.Normalize();

        Assert.Equal(99, preferences.SchemaVersion);
    }

    private static string Snapshot(ShellPreferences preferences) =>
        string.Join(
            "|",
            preferences.SchemaVersion,
            preferences.Width,
            preferences.Opacity,
            preferences.AccentColor,
            preferences.RecentConversationMessageLimit,
            preferences.VoiceInputDeviceNumber,
            preferences.WakeWordPhrase,
            preferences.WakeWordSensitivity,
            preferences.AiProvider,
            preferences.AiBaseUrl,
            preferences.AiModel,
            string.Join(",", preferences.WakeWordAliases));
}
