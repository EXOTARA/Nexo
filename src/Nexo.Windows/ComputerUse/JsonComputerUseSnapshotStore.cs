using System.Text.Json;
using Nexo.Core.ComputerUse;
using Nexo.Core.Diagnostics;

namespace Nexo.Windows.ComputerUse;

/// <summary>
/// Diseño D18 — los pasos que Sakura puede deshacer, en disco. Si Sakura se cierra después de
/// cambiar el portapapeles, deshacerlo en la siguiente sesión sigue siendo posible. Mismo patrón
/// atómico que el resto de stores.
/// </summary>
public sealed class JsonComputerUseSnapshotStore : IComputerUseSnapshotStore
{
    /// <summary>
    /// Pocos y recientes. Cada uno guarda el contenido anterior del portapapeles, que puede ser
    /// largo, y deshacer un paso de hace tres días no es algo que nadie quiera.
    /// </summary>
    public const int MaximumSnapshots = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonComputerUseSnapshotStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.ComputerUseSnapshots;
    }

    public IReadOnlyList<ComputerUseSnapshot> Read()
    {
        lock (_sync)
        {
            return LoadLocked()
                .OrderByDescending(snapshot => snapshot.At)
                .Select(snapshot => snapshot.Copy())
                .ToArray();
        }
    }

    public void Save(ComputerUseSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_sync)
        {
            var all = LoadLocked();
            all.Add(snapshot.Copy());

            SaveLocked(all
                .OrderByDescending(entry => entry.At)
                .Take(MaximumSnapshots)
                .ToList());
        }
    }

    public void Remove(Guid snapshotId)
    {
        lock (_sync)
        {
            var all = LoadLocked();
            if (all.RemoveAll(snapshot => snapshot.Id == snapshotId) > 0)
            {
                SaveLocked(all);
            }
        }
    }

    private List<ComputerUseSnapshot> LoadLocked()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ComputerUseSnapshot>>(
                File.ReadAllText(_filePath), JsonOptions) ?? [];
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private void SaveLocked(List<ComputerUseSnapshot> snapshots)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshots, JsonOptions));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // El coordinador relee antes de dar por hecho nada: si esto falló, no habrá snapshot que
            // encontrar y el paso no se ofrecerá como reversible.
        }
    }
}
