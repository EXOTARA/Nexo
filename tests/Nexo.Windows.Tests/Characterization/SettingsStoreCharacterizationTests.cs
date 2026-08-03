using Nexo.Core.Settings;
using Nexo.Core.Voice;
using Nexo.Windows.Settings;
using Nexo.Windows.Storage;

namespace Nexo.Windows.Tests.Characterization;

/// <summary>
/// Fase 1.1 — congela la conducta real de <see cref="JsonSettingsStore"/> en disco.
///
/// Cubre el escenario 4 de `TEST_MATRIX.md` (**configuración corrupta**, prueba de seguridad
/// bloqueante de RC): arrancar con valores seguros, avisar y **no borrar nada**.
/// </summary>
public sealed class SettingsStoreCharacterizationTests : IDisposable
{
    private readonly string _directory;
    private readonly string _settingsPath;

    public SettingsStoreCharacterizationTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "kohana-characterization",
            Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_directory);
        _settingsPath = Path.Combine(_directory, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void MissingFile_LoadsSafeDefaultsWithoutCreatingAnything()
    {
        var store = new JsonSettingsStore(_settingsPath);

        var preferences = store.Load();

        Assert.False(File.Exists(_settingsPath));
        Assert.False(preferences.HasCompletedOnboarding);

        // Una instalación nueva declara el esquema actual, no el 0. Las migraciones sirven para
        // actualizar archivos viejos; aquí no hay nada que migrar, y decir "versión 0" hacía que
        // el siguiente `Save` reejecutara todas las migraciones y borrara lo recién elegido.
        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheUsersChoices()
    {
        var store = new JsonSettingsStore(_settingsPath);
        var saved = new ShellPreferences
        {
            SchemaVersion = 16,
            HasCompletedOnboarding = true,
            WakeWordEnabled = true,
            WakeWordPhrase = WakeWordPhrase.Kohana,
            AccentColor = "#123456",
            Width = 750
        };

        store.Save(saved);
        var loaded = new JsonSettingsStore(_settingsPath).Load();

        Assert.True(loaded.HasCompletedOnboarding);
        Assert.True(loaded.WakeWordEnabled);
        Assert.Equal(WakeWordPhrase.Kohana, loaded.WakeWordPhrase);
        Assert.Equal("#123456", loaded.AccentColor);
        Assert.Equal(750, loaded.Width);
    }

    [Fact]
    public void Save_CreatesTheDirectoryWhenItDoesNotExist()
    {
        var nestedPath = Path.Combine(_directory, "nested", "deeper", "settings.json");
        var store = new JsonSettingsStore(nestedPath);

        store.Save(new ShellPreferences());

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void Save_LeavesNoTemporaryFileBehind()
    {
        // La escritura es atómica: `.tmp` + `File.Move`.
        var store = new JsonSettingsStore(_settingsPath);

        store.Save(new ShellPreferences());

        Assert.True(File.Exists(_settingsPath));
        Assert.False(File.Exists(_settingsPath + ".tmp"));
    }

    [Fact]
    public void Save_NormalizesBeforeWriting()
    {
        var store = new JsonSettingsStore(_settingsPath);

        store.Save(new ShellPreferences { SchemaVersion = 0, Width = 10_000 });

        var loaded = new JsonSettingsStore(_settingsPath).Load();
        Assert.Equal(ShellPreferences.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(820, loaded.Width);
    }

    [Fact]
    public void CorruptJson_LoadsSafeDefaultsInsteadOfThrowing()
    {
        File.WriteAllText(_settingsPath, "{ esto no es json valido ");
        var store = new JsonSettingsStore(_settingsPath);

        var preferences = store.Load();

        // No lanza y los valores son seguros: es lo que exige el escenario 4.
        Assert.False(preferences.HasCompletedOnboarding);
        Assert.True(preferences.MinimizeToTray);

        // Igual que en el archivo ausente: tras perder el archivo no hay nada que migrar, así que
        // se parte del esquema actual y el siguiente `Save` conserva lo que se le asigne.
        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
    }

    [Fact]
    public void CorruptJson_IsPreservedAsABackup_NeverDeleted()
    {
        // Escenario 4 de TEST_MATRIX: "no borra". El archivo dañado se conserva con
        // sufijo `.corrupt-<marca de tiempo>` para que el usuario pueda recuperarlo.
        File.WriteAllText(_settingsPath, "{ roto ");

        new JsonSettingsStore(_settingsPath).Load();

        var backups = Directory.GetFiles(_directory, "settings.json.corrupt-*");
        Assert.Single(backups);
        Assert.Contains("roto", File.ReadAllText(backups[0]), StringComparison.Ordinal);

        // El primario desaparece para que el siguiente arranque parta de valores seguros.
        Assert.False(File.Exists(_settingsPath));
    }

    [Fact]
    public void AfterCorruption_TheNextSaveKeepsWhatWasJustAssigned()
    {
        // Esta prueba congelaba un defecto de la Fase 1.1: `Load` devolvía `SchemaVersion = 0`
        // tras un archivo corrupto, el siguiente `Save` reejecutaba todas las migraciones desde
        // cero —incluida la de v10, `HasCompletedOnboarding = false`— y perdía lo asignado justo
        // antes de guardar. Se dio por aceptable razonando solo sobre el caso de corrupción, donde
        // los datos ya estaban ilegibles.
        //
        // El razonamiento dejaba fuera el caso que de verdad importa y que comparte exactamente la
        // misma raíz: la **instalación nueva**. Ahí no hay archivo, `Load` devolvía lo mismo, y las
        // elecciones del asistente de bienvenida (proveedor de IA, modelo, onboarding completado)
        // se borraban en el primer guardado — el asistente reaparecía al siguiente arranque.
        File.WriteAllText(_settingsPath, "{ roto ");
        var store = new JsonSettingsStore(_settingsPath);

        var preferences = store.Load();
        preferences.HasCompletedOnboarding = true;
        store.Save(preferences);

        var reloaded = new JsonSettingsStore(_settingsPath).Load();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, reloaded.SchemaVersion);
        Assert.True(reloaded.HasCompletedOnboarding);
    }

    [Fact]
    public void AfterCorruption_ASecondSaveCyclePersistsNormally()
    {
        // Una vez que el archivo vuelve a estar en el esquema 17, guardar funciona como
        // siempre. La degradación es de un solo ciclo.
        File.WriteAllText(_settingsPath, "{ roto ");
        var store = new JsonSettingsStore(_settingsPath);

        store.Save(store.Load());

        var preferences = store.Load();
        preferences.HasCompletedOnboarding = true;
        store.Save(preferences);

        Assert.True(new JsonSettingsStore(_settingsPath).Load().HasCompletedOnboarding);
    }

    [Fact]
    public void JsonLiteralNull_LoadsSafeDefaults()
    {
        // `JsonSerializer.Deserialize` devuelve null sin lanzar: la tienda cae al
        // constructor por defecto y **no** genera copia `.corrupt`.
        File.WriteAllText(_settingsPath, "null");

        var preferences = new JsonSettingsStore(_settingsPath).Load();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.Empty(Directory.GetFiles(_directory, "settings.json.corrupt-*"));
    }

    [Fact]
    public void AnOldSchemaOnDisk_IsMigratedOnLoad()
    {
        File.WriteAllText(
            _settingsPath,
            """
            {
              "SchemaVersion": 8,
              "StartWithWindows": true,
              "MinimizeToTray": false,
              "AccentColor": "#8B6CFF"
            }
            """);

        var preferences = new JsonSettingsStore(_settingsPath).Load();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.False(preferences.StartWithWindows);
        Assert.True(preferences.MinimizeToTray);
        Assert.Equal("#E98AAF", preferences.AccentColor);
    }

    [Fact]
    public void AnOldSchemaOnDisk_KeepsWakeWordAliases()
    {
        File.WriteAllText(
            _settingsPath,
            """
            {
              "SchemaVersion": 15,
              "WakeWordAliases": ["kojana", "cojana"]
            }
            """);

        var preferences = new JsonSettingsStore(_settingsPath).Load();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.NotEmpty(preferences.WakeWordAliases);
    }

    [Fact]
    public void UnknownPropertiesOnDisk_AreIgnoredNotFatal()
    {
        // Un archivo escrito por una versión más nueva no debe romper una versión anterior.
        File.WriteAllText(
            _settingsPath,
            """
            { "SchemaVersion": 16, "UnaPropiedadDelFuturo": 42 }
            """);

        var preferences = new JsonSettingsStore(_settingsPath).Load();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
    }

    [Fact]
    public void CorruptFileBackup_ReturnsNullWhenThereIsNothingToPreserve()
    {
        Assert.Null(CorruptFileBackup.TryPreserve(Path.Combine(_directory, "no-existe.json")));
    }

    [Theory]
    [InlineData(Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Automatic)]
    [InlineData(Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Eco)]
    [InlineData(Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Balanced)]
    [InlineData(Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Maximum)]
    public void HardwarePerformanceMode_RoundTripsThroughSaveAndLoad(
        Nexo.Core.AdaptiveEngine.HardwarePerformanceMode mode)
    {
        var store = new JsonSettingsStore(_settingsPath);
        var saved = new ShellPreferences { SchemaVersion = 17, HardwarePerformanceMode = mode };

        store.Save(saved);
        var loaded = new JsonSettingsStore(_settingsPath).Load();

        Assert.Equal(mode, loaded.HardwarePerformanceMode);
    }

    [Fact]
    public void HardwarePerformanceMode_MissingFromOlderFile_MigratesToAutomatic()
    {
        File.WriteAllText(
            _settingsPath,
            """
            {
              "SchemaVersion": 16
            }
            """);

        var preferences = new JsonSettingsStore(_settingsPath).Load();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.Equal(
            Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Automatic,
            preferences.HardwarePerformanceMode);
    }

    [Fact]
    public void HardwarePerformanceMode_InvalidValueOnDisk_FallsBackToAutomaticWithoutThrowing()
    {
        File.WriteAllText(
            _settingsPath,
            """
            {
              "SchemaVersion": 17,
              "HardwarePerformanceMode": 99
            }
            """);

        var preferences = new JsonSettingsStore(_settingsPath).Load();

        Assert.Equal(
            Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Automatic,
            preferences.HardwarePerformanceMode);
    }
}
