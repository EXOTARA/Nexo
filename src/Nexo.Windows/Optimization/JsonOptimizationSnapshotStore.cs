using System.Text.Json;
using Nexo.Core.Diagnostics;
using Nexo.Core.Optimization;

namespace Nexo.Windows.Optimization;

/// <summary>
/// Diseño D8 — guarda el estado previo en disco, no en memoria: si Sakura se cierra (o se cae)
/// después de aplicar un plan, el usuario tiene que poder deshacerlo en la siguiente sesión. Un
/// snapshot que solo viviera en RAM dejaría de existir justo cuando más falta hace. Sigue el mismo
/// patrón atómico de escritura que el resto de stores del proyecto.
/// </summary>
public sealed class JsonOptimizationSnapshotStore : IOptimizationSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonOptimizationSnapshotStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.OptimizationSnapshot;
    }

    public OptimizationSnapshot? Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<OptimizationSnapshot>(
                    File.ReadAllText(_filePath), JsonOptions);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }
    }

    public void Save(OptimizationSnapshot? snapshot)
    {
        lock (_sync)
        {
            if (snapshot is null)
            {
                TryDelete();
                return;
            }

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshot, JsonOptions));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
    }

    private void TryDelete()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // No poder borrar el snapshot no debe impedir seguir usando Sakura.
        }
    }
}
