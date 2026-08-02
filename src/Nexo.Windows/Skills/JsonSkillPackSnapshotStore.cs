using System.Text.Json;
using Nexo.Core.Diagnostics;
using Nexo.Core.Skills;

namespace Nexo.Windows.Skills;

/// <summary>
/// Diseño D15 — el estado anterior a activar un pack, en disco. Si Kohana se cierra con un pack
/// puesto, desactivarlo en la siguiente sesión tiene que devolver los ajustes reales de la persona,
/// no unos por omisión. Mismo patrón atómico que el resto de stores.
/// </summary>
public sealed class JsonSkillPackSnapshotStore : ISkillPackSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonSkillPackSnapshotStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.SkillPackSnapshot;
    }

    public SkillPackSnapshot? Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<SkillPackSnapshot>(
                    File.ReadAllText(_filePath), JsonOptions);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }
    }

    public void Save(SkillPackSnapshot? snapshot)
    {
        lock (_sync)
        {
            if (snapshot is null)
            {
                TryDelete();
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temporaryPath = _filePath + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshot, JsonOptions));
                File.Move(temporaryPath, _filePath, overwrite: true);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // Sin snapshot no se podría desactivar el pack, pero tampoco se pierde nada
                // irreversible: los ajustes siguen siendo editables a mano en Personalizar.
            }
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
            // No poder borrar la marca del pack no debe impedir seguir usando Kohana.
        }
    }
}
