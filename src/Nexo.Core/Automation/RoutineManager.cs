namespace Nexo.Core.Automation;

public sealed class RoutineManager
{
    private readonly object _sync = new();
    private readonly IRoutineStore _store;
    private RoutineState _state = new();

    public RoutineManager(IRoutineStore store)
    {
        _store = store;
    }

    public void Load()
    {
        lock (_sync)
        {
            _state = Normalize(_store.Load());
            if (_state.Routines.Count == 0)
            {
                _state.Routines = CreateDefaults();
                SaveLocked();
            }
        }
    }

    public IReadOnlyList<RoutineDefinition> GetAll()
    {
        lock (_sync)
        {
            return _state.Routines
                .OrderByDescending(routine => routine.IsEnabled)
                .ThenBy(routine => routine.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(routine => routine.Copy())
                .ToArray();
        }
    }

    public RoutineDefinition? FindBestMatch(string query)
    {
        var normalized = RoutineText.Normalize(query);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        lock (_sync)
        {
            var exact = _state.Routines.FirstOrDefault(routine =>
                routine.IsEnabled &&
                (RoutineText.Normalize(routine.TriggerPhrase) == normalized ||
                 RoutineText.Normalize(routine.Name) == normalized));
            if (exact is not null)
            {
                return exact.Copy();
            }

            return _state.Routines
                .Where(routine => routine.IsEnabled)
                .Select(routine => new
                {
                    Routine = routine,
                    Name = RoutineText.Normalize(routine.Name),
                    Trigger = RoutineText.Normalize(routine.TriggerPhrase)
                })
                .Where(candidate =>
                    (!string.IsNullOrWhiteSpace(candidate.Name) && normalized.Contains(candidate.Name)) ||
                    (!string.IsNullOrWhiteSpace(candidate.Trigger) && normalized.Contains(candidate.Trigger)))
                .OrderByDescending(candidate => Math.Max(candidate.Name.Length, candidate.Trigger.Length))
                .Select(candidate => candidate.Routine.Copy())
                .FirstOrDefault();
        }
    }

    public RoutineOperationResult Create(RoutineDefinition routine)
    {
        if (!TryNormalizeRoutine(routine, out var normalized, out var error))
        {
            return RoutineOperationResult.Failed(error);
        }

        lock (_sync)
        {
            normalized.Id = Guid.NewGuid();
            _state.Routines.Add(normalized);
            SaveLocked();
            return RoutineOperationResult.Completed($"Creé la rutina {normalized.Name}.", normalized);
        }
    }

    public RoutineOperationResult Update(RoutineDefinition routine)
    {
        if (!TryNormalizeRoutine(routine, out var normalized, out var error))
        {
            return RoutineOperationResult.Failed(error);
        }

        lock (_sync)
        {
            var index = _state.Routines.FindIndex(candidate => candidate.Id == normalized.Id);
            if (index < 0)
            {
                return RoutineOperationResult.Failed("La rutina ya no existe.");
            }

            _state.Routines[index] = normalized;
            SaveLocked();
            return RoutineOperationResult.Completed($"Actualicé la rutina {normalized.Name}.", normalized);
        }
    }

    /// <summary>
    /// Diseño D3: alterna habilitada/deshabilitada sin pasar por el editor completo — la lista de
    /// rutinas ya muestra el estado, así que cambiarlo ahí mismo evita el rodeo de abrir, tocar un
    /// checkbox y guardar para una acción de un solo campo.
    /// </summary>
    public RoutineOperationResult SetEnabled(Guid id, bool enabled)
    {
        lock (_sync)
        {
            var routine = _state.Routines.FirstOrDefault(candidate => candidate.Id == id);
            if (routine is null)
            {
                return RoutineOperationResult.Failed("La rutina ya no existe.");
            }

            routine.IsEnabled = enabled;
            SaveLocked();
            return RoutineOperationResult.Completed(
                enabled ? $"Activé la rutina {routine.Name}." : $"Desactivé la rutina {routine.Name}.",
                routine.Copy());
        }
    }

    /// <summary>
    /// Diseño D3: registra el resultado de una ejecución ya realizada por <see cref="RoutineRunner"/>.
    /// Se llama desde la capa de UI justo después de <c>RunAsync</c>; el runner en sí no conoce el
    /// registro de rutinas (no le corresponde) y sigue sin cambios.
    /// </summary>
    public void RecordExecution(Guid id, DateTimeOffset completedAt, bool succeeded)
    {
        lock (_sync)
        {
            var routine = _state.Routines.FirstOrDefault(candidate => candidate.Id == id);
            if (routine is null)
            {
                return;
            }

            routine.LastExecutedAt = completedAt;
            routine.LastExecutionSucceeded = succeeded;
            SaveLocked();
        }
    }

    public RoutineOperationResult Delete(Guid id)
    {
        lock (_sync)
        {
            var routine = _state.Routines.FirstOrDefault(candidate => candidate.Id == id);
            if (routine is null)
            {
                return RoutineOperationResult.Failed("La rutina ya no existe.");
            }

            _state.Routines.Remove(routine);
            SaveLocked();
            return RoutineOperationResult.Completed($"Eliminé la rutina {routine.Name}.", routine);
        }
    }

    private void SaveLocked() => _store.Save(_state.Copy());

    private static RoutineState Normalize(RoutineState? state)
    {
        state ??= new RoutineState();

        if (state.SchemaVersion < 1)
        {
            state.SchemaVersion = 1;
        }

        if (state.SchemaVersion < 2)
        {
            // Diseño D3: LastExecutedAt/LastExecutionSucceeded son nuevos y anulables — un
            // archivo v1 los deserializa como null, que ya es el valor correcto para "nunca
            // ejecutada todavía". No hay dato que transformar; el escalón solo registra que este
            // archivo pasó por la revisión de D3.
            state.SchemaVersion = 2;
        }

        state.Routines ??= [];
        state.Routines = state.Routines
            .Select(routine => TryNormalizeRoutine(routine, out var normalized, out _)
                ? normalized
                : null)
            .Where(routine => routine is not null)
            .Cast<RoutineDefinition>()
            .ToList();
        return state;
    }

    private static bool TryNormalizeRoutine(
        RoutineDefinition source,
        out RoutineDefinition routine,
        out string error)
    {
        routine = source.Copy();
        error = string.Empty;

        routine.Name = routine.Name.Trim();
        routine.TriggerPhrase = routine.TriggerPhrase.Trim();
        routine.Steps ??= [];
        routine.Steps = routine.Steps.Select(step => step.Copy()).ToList();

        if (string.IsNullOrWhiteSpace(routine.Name))
        {
            error = "La rutina necesita un nombre.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(routine.TriggerPhrase))
        {
            error = "La rutina necesita una frase de activación.";
            return false;
        }

        if (routine.Steps.Count == 0)
        {
            error = "Agrega al menos una acción a la rutina.";
            return false;
        }

        foreach (var step in routine.Steps)
        {
            if (!AutomationPermissionPolicy.IsAllowed(step, out error))
            {
                return false;
            }
        }

        return true;
    }

    private static List<RoutineDefinition> CreateDefaults() =>
    [
        new RoutineDefinition
        {
            Name = "Programación",
            TriggerPhrase = "modo programación",
            RequiresConfirmation = true,
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.OpenApplication,
                    Target = "code",
                    Arguments = ".",
                    WorkingDirectory = "{project}"
                },
                new AutomationAction
                {
                    Type = AutomationActionType.OpenTerminal,
                    WorkingDirectory = "{project}"
                },
                new AutomationAction
                {
                    Type = AutomationActionType.SetApplicationVolume,
                    Target = "Spotify",
                    NumericValue = 20
                },
                new AutomationAction
                {
                    Type = AutomationActionType.MuteApplication,
                    Target = "Discord"
                },
                new AutomationAction
                {
                    Type = AutomationActionType.StartFocus,
                    NumericValue = 50,
                    Text = "Sesión de programación"
                }
            ]
        },
        new RoutineDefinition
        {
            Name = "Estudio",
            TriggerPhrase = "modo estudio",
            RequiresConfirmation = false,
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.SetApplicationVolume,
                    Target = "Spotify",
                    NumericValue = 15
                },
                new AutomationAction
                {
                    Type = AutomationActionType.MuteApplication,
                    Target = "Discord"
                },
                new AutomationAction
                {
                    Type = AutomationActionType.StartFocus,
                    NumericValue = 40,
                    Text = "Sesión de estudio"
                }
            ]
        },
        new RoutineDefinition
        {
            Name = "Descanso",
            TriggerPhrase = "modo descanso",
            RequiresConfirmation = false,
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.SetApplicationVolume,
                    Target = "Spotify",
                    NumericValue = 35
                },
                new AutomationAction
                {
                    Type = AutomationActionType.UnmuteApplication,
                    Target = "Discord"
                },
                new AutomationAction
                {
                    Type = AutomationActionType.StartBreak,
                    NumericValue = 10,
                    Text = "Descanso"
                }
            ]
        }
    ];
}
