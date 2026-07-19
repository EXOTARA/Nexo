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
                return new ShellPreferences();
            }

            var json = File.ReadAllText(_settingsPath);
            var preferences = JsonSerializer.Deserialize<ShellPreferences>(json) ?? new ShellPreferences();
            preferences.Normalize();
            return preferences;
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            CorruptFileBackup.TryPreserve(_settingsPath);
            return new ShellPreferences();
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
}
