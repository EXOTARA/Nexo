using System.Text.Json;
using System.Text.Json.Serialization;
using Nexo.Core.Audit;
using Nexo.Core.Diagnostics;

namespace Nexo.Windows.Audit;

/// <summary>
/// Diseño D13 — el Audit Log único en disco. Sobrevive al cierre de Sakura a propósito: un registro
/// que se pierde al reiniciar no sirve para explicar por qué el equipo está como está, que es justo
/// para lo que existe.
///
/// Sin cifrar, y es deliberado: aquí no hay datos personales, solo qué hizo Sakura y cuándo. Que la
/// persona pueda abrir el archivo con cualquier editor es parte de que la auditoría sea suya. (La
/// memoria personal, que sí contiene datos suyos, va cifrada con DPAPI — ver D9.)
/// </summary>
public sealed class JsonSakuraAuditLog : IAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _sync = new();
    private readonly string _filePath;
    private readonly string? _legacyOptimizationPath;

    public JsonSakuraAuditLog(string? filePath = null, string? legacyOptimizationPath = null)
    {
        _filePath = filePath ?? NexoDataPaths.Audit;
        _legacyOptimizationPath = legacyOptimizationPath ?? NexoDataPaths.OptimizationAudit;
    }

    public IReadOnlyList<AuditEntry> Read()
    {
        lock (_sync)
        {
            return LoadLocked().Entries
                .OrderByDescending(entry => entry.At)
                .Select(entry => entry.Copy())
                .ToArray();
        }
    }

    public void Append(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_sync)
        {
            var state = LoadLocked();
            state.Entries.Add(entry.Copy());

            // Recorte por antigüedad, nunca selectivo: poder quitar UNA entrada convertiría el
            // registro en una versión de los hechos.
            if (state.Entries.Count > AuditState.MaximumEntries)
            {
                state.Entries = state.Entries
                    .OrderByDescending(existing => existing.At)
                    .Take(AuditState.MaximumEntries)
                    .ToList();
            }

            SaveLocked(state);
        }
    }

    private AuditState LoadLocked()
    {
        if (File.Exists(_filePath))
        {
            return ReadStateOrEmpty(_filePath);
        }

        // Diseño D13 — el registro de D11 vivía en su propio archivo. Al unificar, lo ya escrito se
        // importa en vez de abandonarse: una entrada de auditoría que desaparece porque el formato
        // cambió es exactamente lo que un registro no puede permitirse.
        var imported = ImportLegacyOptimizationEntries();
        if (imported.Entries.Count > 0)
        {
            SaveLocked(imported);
        }

        return imported;
    }

    private AuditState ReadStateOrEmpty(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<AuditState>(File.ReadAllText(path), JsonOptions)
                ?? new AuditState();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AuditState();
        }
    }

    private AuditState ImportLegacyOptimizationEntries()
    {
        if (string.IsNullOrWhiteSpace(_legacyOptimizationPath) || !File.Exists(_legacyOptimizationPath))
        {
            return new AuditState();
        }

        try
        {
            var legacy = JsonSerializer.Deserialize<LegacyOptimizationAuditState>(
                File.ReadAllText(_legacyOptimizationPath), JsonOptions);

            if (legacy?.Entries is null)
            {
                return new AuditState();
            }

            return new AuditState
            {
                Entries = legacy.Entries.Select(entry => new AuditEntry
                {
                    At = entry.At,
                    Capability = AuditCapability.Optimizacion,
                    Action = $"{entry.Action} ({entry.Scenario})",
                    Detail = entry.Detail ?? string.Empty,
                    Permission = "Optimización confirmada por el usuario"
                }).ToList()
            };
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AuditState();
        }
    }

    private void SaveLocked(AuditState state)
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
            // No poder escribir la auditoría no debe impedir la acción: el registro acompaña a lo
            // que se hace, no lo gobierna.
        }
    }

    /// <summary>Forma del archivo de D11, solo para poder importarlo.</summary>
    private sealed class LegacyOptimizationAuditState
    {
        public List<LegacyOptimizationAuditEntry> Entries { get; set; } = [];
    }

    private sealed class LegacyOptimizationAuditEntry
    {
        public DateTimeOffset At { get; set; }

        public string Scenario { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string? Detail { get; set; }
    }
}
