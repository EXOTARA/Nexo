namespace Nexo.Core.Memory;

/// <summary>
/// Diseño D9 (Fase 6) — la única puerta para escribir y leer memoria. Toda escritura pasa
/// obligatoriamente por <see cref="MemoryPolicy"/>: no existe un camino que guarde saltándose los
/// controles, que es justo lo que exige el criterio de terminado de la fase.
///
/// Búsqueda literal primero, por decisión ya registrada en el roadmap: nada de índices semánticos
/// hasta que haya un benchmark y una licencia verificada del modelo de embeddings.
/// </summary>
public sealed class MemoryManager
{
    private readonly object _sync = new();
    private readonly IMemoryStore _store;
    private MemoryState _state = new();

    public MemoryManager(IMemoryStore store)
    {
        _store = store;
    }

    public void Load()
    {
        lock (_sync)
        {
            var loaded = _store.Load() ?? new MemoryState();
            loaded.Entries ??= [];
            _state = loaded;
        }
    }

    public MemoryOperationResult Remember(
        MemoryCategory category,
        string? text,
        MemorySettings settings,
        DateTimeOffset now)
    {
        var verdict = MemoryPolicy.CanRemember(category, text, settings);
        if (!verdict.Success)
        {
            return verdict;
        }

        lock (_sync)
        {
            _state.Entries.Add(new MemoryEntry
            {
                Id = Guid.NewGuid(),
                Category = category,
                Text = text!.Trim(),
                CreatedAt = now
            });

            _state.Entries = [.. MemoryPolicy.ApplyRetention(_state.Entries, settings, now)];
            SaveLocked();
        }

        return MemoryOperationResult.Completed("Lo recordaré.");
    }

    /// <summary>
    /// Diseño D9 — la retención se aplica también AL LEER, no solo al escribir. Si la persona baja
    /// la retención de 90 a 7 días, lo viejo debe dejar de existir en ese momento, sin esperar a
    /// que algo nuevo se guarde: acortar la retención es una orden, no una preferencia futura.
    /// </summary>
    public IReadOnlyList<MemoryEntry> GetAll(MemorySettings settings, DateTimeOffset now)
    {
        lock (_sync)
        {
            var retained = MemoryPolicy.ApplyRetention(_state.Entries, settings, now);
            if (retained.Count != _state.Entries.Count)
            {
                _state.Entries = [.. retained];
                SaveLocked();
            }

            return retained.Select(entry => entry.Copy()).ToArray();
        }
    }

    /// <summary>Búsqueda literal, sin distinguir mayúsculas ni acentos de más.</summary>
    public IReadOnlyList<MemoryEntry> Search(string? query, MemorySettings settings, DateTimeOffset now)
    {
        var all = GetAll(settings, now);
        if (string.IsNullOrWhiteSpace(query))
        {
            return all;
        }

        return all
            .Where(entry => entry.Text.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public MemoryOperationResult Forget(Guid entryId)
    {
        lock (_sync)
        {
            var removed = _state.Entries.RemoveAll(entry => entry.Id == entryId);
            if (removed == 0)
            {
                return MemoryOperationResult.Rejected("No encontré esa entrada.");
            }

            SaveLocked();
            return MemoryOperationResult.Completed("Lo olvidé.");
        }
    }

    /// <summary>Borrado total. Debe funcionar aunque la memoria esté desactivada.</summary>
    public MemoryOperationResult ForgetEverything()
    {
        lock (_sync)
        {
            var count = _state.Entries.Count;
            _state.Entries.Clear();
            SaveLocked();
            return MemoryOperationResult.Completed(
                count == 0 ? "No había nada guardado." : $"Olvidé {count} cosas.");
        }
    }

    private void SaveLocked() => _store.Save(_state.Copy());
}
