using System.Text.Json;
using System.Text.Json.Serialization;
using Nexo.Core.Diagnostics;
using Nexo.Core.Optimization;

namespace Nexo.Windows.Optimization;

/// <summary>
/// Diseño D11 — la auditoría en disco. Sobrevive al cierre de Kohana a propósito: un registro que se
/// pierde al reiniciar no sirve para explicar por qué el equipo está como está, que es justo para lo
/// que existe.
///
/// Sin cifrar, y es deliberado: aquí no hay datos personales, solo qué ajuste se cambió y cuándo.
/// Que la persona pueda abrir el archivo y leerlo con cualquier editor es parte de que la auditoría
/// sea suya. (La memoria personal, que sí contiene datos suyos, va cifrada con DPAPI — ver D9.)
/// </summary>
public sealed class JsonOptimizationAuditLog : IOptimizationAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonOptimizationAuditLog(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.OptimizationAudit;
    }

    public IReadOnlyList<OptimizationAuditEntry> Read()
    {
        lock (_sync)
        {
            return LoadLocked().Entries
                .OrderByDescending(entry => entry.At)
                .Select(entry => entry.Copy())
                .ToArray();
        }
    }

    public void Append(OptimizationAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_sync)
        {
            var state = LoadLocked();
            state.Entries.Add(entry.Copy());

            // Recorte por antigüedad, nunca selectivo: poder quitar UNA entrada convertiría el
            // registro en una versión de los hechos.
            if (state.Entries.Count > OptimizationAuditState.MaximumEntries)
            {
                state.Entries = state.Entries
                    .OrderByDescending(existing => existing.At)
                    .Take(OptimizationAuditState.MaximumEntries)
                    .ToList();
            }

            SaveLocked(state);
        }
    }

    private OptimizationAuditState LoadLocked()
    {
        if (!File.Exists(_filePath))
        {
            return new OptimizationAuditState();
        }

        try
        {
            return JsonSerializer.Deserialize<OptimizationAuditState>(
                File.ReadAllText(_filePath), JsonOptions) ?? new OptimizationAuditState();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new OptimizationAuditState();
        }
    }

    private void SaveLocked(OptimizationAuditState state)
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
            // No poder escribir la auditoría no debe impedir aplicar ni deshacer una optimización:
            // el registro acompaña a la acción, no la gobierna.
        }
    }
}
