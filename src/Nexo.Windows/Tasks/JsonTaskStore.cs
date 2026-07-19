using System.Text.Json;
using Nexo.Core.Diagnostics;
using Nexo.Core.Tasks;

namespace Nexo.Windows.Tasks;

public sealed class JsonTaskStore : ITaskStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonTaskStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.Tasks;
    }

    public IReadOnlyList<NexoTask> Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<NexoTask>>(json, JsonOptions) ?? [];
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or JsonException)
            {
                PreserveCorruptFile();
                return [];
            }
        }
    }

    public void Save(IReadOnlyCollection<NexoTask> tasks)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(tasks, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
    }

    private void PreserveCorruptFile()
    {
        try
        {
            var backupPath = _filePath + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(_filePath, backupPath, overwrite: true);
        }
        catch
        {
            // Si el respaldo también falla, Nexo continúa con una lista vacía.
        }
    }
}
