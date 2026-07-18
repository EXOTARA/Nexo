using System.Text.Json;
using Nexo.Core.Settings;

namespace Nexo.Windows.Settings;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public JsonSettingsStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsPath = Path.Combine(localAppData, "Nexo", "settings.json");
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
        catch (JsonException)
        {
            return new ShellPreferences();
        }
        catch (IOException)
        {
            return new ShellPreferences();
        }
        catch (UnauthorizedAccessException)
        {
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
        File.WriteAllText(_settingsPath, json);
    }
}
