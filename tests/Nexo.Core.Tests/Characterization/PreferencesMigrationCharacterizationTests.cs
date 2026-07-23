using Nexo.Core.Ai;
using Nexo.Core.Settings;
using Nexo.Core.Voice;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Fase 1.1 — congela las migraciones de preferencias (esquema v16) y los saneamientos que
/// <see cref="ShellPreferences.Normalize"/> aplica en cada carga.
///
/// Regla de `MIGRATION_PLAN.md`: cada incremento es aditivo, con default seguro, y **nunca**
/// borra datos del usuario.
/// </summary>
public sealed class PreferencesMigrationCharacterizationTests
{
    private const int CurrentSchemaVersion = 16;

    [Fact]
    public void CurrentSchemaVersion_IsSixteen()
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
    public void AnyOlderSchema_MigratesForwardToSixteen(int startingVersion)
    {
        var preferences = new ShellPreferences { SchemaVersion = startingVersion };

        preferences.Normalize();

        Assert.Equal(CurrentSchemaVersion, preferences.SchemaVersion);
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

    [Theory]
    [InlineData(100, 680)]
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

    [Theory]
    [InlineData(0.1, 0.82)]
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

        Assert.Equal(WakeWordPhrase.OyeKohana, preferences.WakeWordPhrase);
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
