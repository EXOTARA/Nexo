using Nexo.Core.Settings;
using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class WakeWordPreferencesTests
{
    [Fact]
    public void Normalize_MigratesVeryOldWakeWordAsDisabled()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 4,
            WakeWordEnabled = true,
            WakeWordPhrase = WakeWordPhrase.OyeNexo
        };

        preferences.Normalize();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.False(preferences.WakeWordEnabled);
        // Diseño D69 — cambiada a conciencia con el cambio de nombre. La frase por omisión es
        // ahora "Oye Sakura": "kohana" no existe en el léxico español del reconocedor y salía
        // escrita "oye co ana", así que conservarla habría sido conservar la única que no
        // funcionaba. La forma —corta, "oye" o "hey"— sí se respeta al migrar.
        Assert.Equal(WakeWordPhrase.OyeSakura, preferences.WakeWordPhrase);
    }

    [Theory]
    [InlineData(WakeWordPhrase.Nexo)]
    [InlineData(WakeWordPhrase.OyeNexo)]
    [InlineData(WakeWordPhrase.HeyNexo)]
    public void Normalize_MigratesLegacyBrandPhrase(WakeWordPhrase legacyPhrase)
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 13,
            WakeWordEnabled = true,
            WakeWordPhrase = legacyPhrase
        };

        preferences.Normalize();

        Assert.True(preferences.WakeWordEnabled);
        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.Equal(WakeWordPhrase.OyeSakura, preferences.WakeWordPhrase);
    }

    [Fact]
    public void Normalize_KeepsTheShapeOfTheChoiceWhenTheNameChanges()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 15,
            WakeWordEnabled = true,
            WakeWordPhrase = WakeWordPhrase.Kohana
        };

        preferences.Normalize();

        Assert.True(preferences.WakeWordEnabled);

        // Diseño D69 — quien había elegido la frase corta se queda con la corta; lo que cambia es
        // el nombre, no la forma de llamarla.
        Assert.Equal(WakeWordPhrase.Sakura, preferences.WakeWordPhrase);
    }

    [Fact]
    public void Normalize_RepairsUnknownWakeWordPhrase()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 15,
            WakeWordPhrase = (WakeWordPhrase)99
        };

        preferences.Normalize();

        Assert.Equal(WakeWordPhrase.OyeSakura, preferences.WakeWordPhrase);
    }

    [Fact]
    public void Normalize_AddsAndSanitizesPersonalAliases()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 15,
            WakeWordAliases = [" CÓYANA ", "coyana", "oye nexo"]
        };

        preferences.Normalize();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.Equal(new[] { "coyana" }, preferences.WakeWordAliases);
    }
}
