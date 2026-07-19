using System.Text.Json;
using Nexo.Core.Automation;
using Nexo.Core.Diagnostics;
using Nexo.Windows.Storage;

namespace Nexo.Windows.Automation;

public sealed class JsonRoutineStore : IRoutineStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonRoutineStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.Routines;
    }

    public RoutineState Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return new RoutineState();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<RoutineState>(json, JsonOptions) ?? new RoutineState();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or JsonException)
            {
                CorruptFileBackup.TryPreserve(_filePath);
                return new RoutineState();
            }
        }
    }

    public void Save(RoutineState state)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(tempPath, _filePath, overwrite: true);
        }
    }
}
