using System.Text.Json;
using Nexo.Core.Diagnostics;
using Nexo.Core.Workspace;

namespace Nexo.Windows.Workspace;

/// <summary>
/// Diseño D14 — los checkpoints en disco, no en memoria. Si Kohana se cierra (o se cae) después de
/// modificar un archivo, la persona tiene que poder deshacerlo en la siguiente sesión: un checkpoint
/// que solo viviera en RAM dejaría de existir justo cuando más falta hace. Mismo patrón atómico de
/// escritura que el resto de stores del proyecto.
/// </summary>
public sealed class JsonWorkspaceCheckpointStore : IWorkspaceCheckpointStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonWorkspaceCheckpointStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.WorkspaceCheckpoints;
    }

    public IReadOnlyList<WorkspaceCheckpoint> Read()
    {
        lock (_sync)
        {
            return LoadLocked().Checkpoints
                .OrderByDescending(checkpoint => checkpoint.At)
                .Select(checkpoint => checkpoint.Copy())
                .ToArray();
        }
    }

    public void Save(WorkspaceCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        lock (_sync)
        {
            var state = LoadLocked();
            state.Checkpoints.Add(checkpoint.Copy());

            // Se van los más viejos. Cada checkpoint guarda un archivo entero, así que sin tope el
            // historial acabaría pesando más que el propio proyecto.
            if (state.Checkpoints.Count > WorkspaceCheckpointState.MaximumCheckpoints)
            {
                state.Checkpoints = state.Checkpoints
                    .OrderByDescending(existing => existing.At)
                    .Take(WorkspaceCheckpointState.MaximumCheckpoints)
                    .ToList();
            }

            SaveLocked(state);
        }
    }

    /// <summary>
    /// Quitar un checkpoint concreto sí está permitido, al revés que en la auditoría: aquí no se
    /// borra el registro de que algo pasó —eso vive en el Audit Log—, solo la copia de seguridad que
    /// ya se usó o que dejó de corresponder a un cambio real.
    /// </summary>
    public void Remove(Guid checkpointId)
    {
        lock (_sync)
        {
            var state = LoadLocked();
            if (state.Checkpoints.RemoveAll(checkpoint => checkpoint.Id == checkpointId) > 0)
            {
                SaveLocked(state);
            }
        }
    }

    private WorkspaceCheckpointState LoadLocked()
    {
        if (!File.Exists(_filePath))
        {
            return new WorkspaceCheckpointState();
        }

        try
        {
            return JsonSerializer.Deserialize<WorkspaceCheckpointState>(
                File.ReadAllText(_filePath), JsonOptions) ?? new WorkspaceCheckpointState();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new WorkspaceCheckpointState();
        }
    }

    private void SaveLocked(WorkspaceCheckpointState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Si el checkpoint no se puede guardar, el coordinador se enterará al releerlo y no
            // aplicará el cambio: sin copia previa no se escribe.
        }
    }
}
