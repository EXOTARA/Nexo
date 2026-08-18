using System.Text.Json;
using Nexo.Core.Diagnostics;
using Nexo.Core.Settings;
using Nexo.Windows.Storage;

namespace Nexo.Windows.Settings;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public JsonSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? NexoDataPaths.Settings;
    }

    public ShellPreferences Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return CreateFreshPreferences();
            }

            var json = File.ReadAllText(_settingsPath);
            var preferences = JsonSerializer.Deserialize<ShellPreferences>(json) ?? CreateFreshPreferences();
            preferences.Normalize();
            return preferences;
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            CorruptFileBackup.TryPreserve(_settingsPath);
            return CreateFreshPreferences();
        }
    }

    public void Save(ShellPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.Normalize();

        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("No se pudo determinar la carpeta de configuración.");

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(preferences, SerializerOptions);
        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }

    /// <summary>
    /// Las migraciones de <see cref="ShellPreferences.Normalize"/> existen para actualizar archivos
    /// viejos, no para inicializar uno nuevo: arrancan desde la versión que declara el archivo y
    /// reimponen los valores por omisión de cada versión intermedia. Un objeto recién construido
    /// declara la versión 0, así que si se guarda tal cual, Normalize lo trata como un archivo
    /// prehistórico y pisa lo que el usuario acabe de elegir. En una instalación nueva no hay nada
    /// que migrar, y eso es justo lo que dice esta versión.
    /// </summary>
    private static ShellPreferences CreateFreshPreferences() =>
        new() { SchemaVersion = ShellPreferences.CurrentSchemaVersion };
}
