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

        // HALLAZGO DE LA FASE 1.1 — asimetría real de `JsonSettingsStore.Load`.
        // La ruta de éxito llama a `preferences.Normalize()`; las rutas de "archivo ausente"
        // y de "archivo corrupto" devuelven `new ShellPreferences()` **sin normalizar**.
        // Por eso el esquema sale en 0 y no en 16.
        Assert.Equal(0, preferences.SchemaVersion);
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
        Assert.Equal(16, loaded.SchemaVersion);
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

        // Misma asimetría que en el archivo ausente: la ruta de recuperación no normaliza.
        Assert.Equal(0, preferences.SchemaVersion);
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
    public void AfterCorruption_TheNextSaveReplaysEveryMigrationFromZero_KnownDefect()
    {
        // HALLAZGO DE LA FASE 1.1 — consecuencia directa de la asimetría anterior.
        //
        // Tras un archivo corrupto, `Load` devuelve preferencias con `SchemaVersion = 0`.
        // El siguiente `Save` llama a `Normalize()`, que **reejecuta todas las migraciones
        // desde 0**, incluida la de v10 (`HasCompletedOnboarding = false`). El resultado es
        // que un valor asignado justo antes de guardar se pierde en el mismo ciclo.
        //
        // Tras un archivo corrupto los valores por defecto son aceptables, así que esto no
        // pierde datos del usuario (ya estaban ilegibles). Pero sí significa que el shell no
        // puede marcar el onboarding como completado en ese mismo arranque.
        File.WriteAllText(_settingsPath, "{ roto ");
        var store = new JsonSettingsStore(_settingsPath);

        var preferences = store.Load();
        preferences.HasCompletedOnboarding = true;
        store.Save(preferences);

        var reloaded = new JsonSettingsStore(_settingsPath).Load();

        Assert.Equal(16, reloaded.SchemaVersion);
        Assert.False(reloaded.HasCompletedOnboarding);
    }

    [Fact]
    public void AfterCorruption_ASecondSaveCyclePersistsNormally()
    {
        // Una vez que el archivo vuelve a estar en el esquema 16, guardar funciona como
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

        Assert.Equal(16, preferences.SchemaVersion);
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

        Assert.Equal(16, preferences.SchemaVersion);
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

        Assert.Equal(16, preferences.SchemaVersion);
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

        Assert.Equal(16, preferences.SchemaVersion);
    }

    [Fact]
    public void CorruptFileBackup_ReturnsNullWhenThereIsNothingToPreserve()
    {
        Assert.Null(CorruptFileBackup.TryPreserve(Path.Combine(_directory, "no-existe.json")));
    }
}
