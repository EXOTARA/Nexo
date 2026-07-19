using System.Text.Json;
using Nexo.Core.Diagnostics;
using Nexo.Core.Focus;

namespace Nexo.Windows.Focus;

public sealed class JsonFocusStore : IFocusStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonFocusStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.Focus;
    }

    public FocusState Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return new FocusState();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<FocusState>(json, JsonOptions) ?? new FocusState();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or JsonException)
            {
                PreserveCorruptFile();
                return new FocusState();
            }
        }
    }

    public void Save(FocusState state)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(state, JsonOptions);
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
            // Un archivo dañado no debe impedir que Nexo abra.
        }
    }
}
