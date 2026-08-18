using Nexo.Core.Audit;
using Nexo.Core.Permissions;

namespace Nexo.Core.ComputerUse;

/// <summary>Diseño D18 — lo que hay que hacer, ya concretado en un método y sus datos.</summary>
public sealed record ComputerUseRequest(
    ComputerUseMethod Method,
    string Description,
    string? ClipboardText = null,
    string? SafeCommandId = null,
    string? TargetApp = null,
    IReadOnlyList<MandatoryConfirmation>? Categories = null,
    long WindowHandle = 0,
    string? ControlName = null)
{
    public IReadOnlyList<MandatoryConfirmation> MandatoryCategories => Categories ?? [];
}

/// <summary>Diseño D18 — el estado anterior, para poder volver. Hoy solo el portapapeles lo tiene.</summary>
public sealed class ComputerUseSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset At { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Contenido anterior del portapapeles. Null si no se pudo leer o no aplica.</summary>
    public string? PreviousClipboard { get; set; }

    /// <summary>Lo que Kohana dejó puesto, para saber si alguien lo cambió después.</summary>
    public string? WrittenClipboard { get; set; }

    public ComputerUseSnapshot Copy() => new()
    {
        Id = Id,
        At = At,
        Description = Description,
        PreviousClipboard = PreviousClipboard,
        WrittenClipboard = WrittenClipboard
    };
}

public interface IComputerUseSnapshotStore
{
    IReadOnlyList<ComputerUseSnapshot> Read();

    void Save(ComputerUseSnapshot snapshot);

    void Remove(Guid snapshotId);
}

public sealed record ComputerUseResult(bool Success, string Detail, string Output, Guid SnapshotId)
{
    public static ComputerUseResult Done(string detail, string output, Guid snapshotId) =>
        new(true, detail, output, snapshotId);

    public static ComputerUseResult Refused(string detail) => new(false, detail, string.Empty, Guid.Empty);
}

/// <summary>
/// Diseño D18 (Fase 7, nivel 4 — "Ejecutar un paso") — el único camino por el que Kohana actúa sobre
/// el equipo. Mismo esqueleto que <c>WorkspaceEditCoordinator</c> en D14, y a propósito: los dos
/// resuelven el mismo problema —ejecutar un paso confirmado sin quedarse a medias— y darle a cada
/// capacidad su propio esqueleto es como acaban divergiendo las garantías.
///
/// El orden:
/// 1. **El Permission Broker decide.** Denegado es denegado; ninguna capacidad se salta al broker.
/// 2. **Nivel 4 o más.** Por debajo, Kohana propone y no toca.
/// 3. **El método pedido tiene que ser el más seguro disponible.** No basta con que sea ejecutable:
///    si hay uno mejor, se rechaza. Si no, el orden estricto de D17 sería decorativo — bastaría con
///    pedir el método cómodo para saltárselo.
/// 4. **Snapshot antes de actuar**, cuando el método lo permita.
/// 5. **Verificación releyendo**, cuando el método lo permita.
/// 6. **Al Audit Log**, con cómo revertirlo.
///
/// Un paso es una acción. Encadenar es el nivel 5, que sigue cerrado.
/// </summary>
public sealed class ComputerUseCoordinator
{
    private readonly IComputerUseExecutor _executor;
    private readonly IComputerUseMethodProbe _probe;
    private readonly IComputerUseSnapshotStore _snapshots;
    private readonly IAuditLog _audit;
    private readonly IUiAutomationInvoker? _invoker;
    private readonly Func<DateTimeOffset> _clock;

    public ComputerUseCoordinator(
        IComputerUseExecutor executor,
        IComputerUseMethodProbe probe,
        IComputerUseSnapshotStore snapshots,
        IAuditLog audit,
        IUiAutomationInvoker? invoker = null,
        Func<DateTimeOffset>? clock = null)
    {
        _executor = executor;
        _probe = probe;
        _snapshots = snapshots;
        _audit = audit;

        // Opcional a propósito: sin invocador, el escalón 5 simplemente no se puede ejecutar y el
        // coordinador lo dice. Es preferible a exigir un doble vacío en todos los sitios que solo
        // usan portapapeles y comandos.
        _invoker = invoker;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public IReadOnlyList<ComputerUseSnapshot> Snapshots => _snapshots.Read();

    /// <summary>
    /// Comprueba si podría ejecutarse, sin ejecutar. Devuelve el motivo cuando no.
    /// </summary>
    public string? FindBlocker(
        ComputerUseRequest request,
        PermissionSettings permissions,
        AutonomyLevel level,
        bool simulatedInputAllowed = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(permissions);

        var decision = PermissionBroker.Decide(
            new PermissionRequest(
                KohanaCapability.ComputerUse,
                request.Description,
                request.TargetApp,
                request.MandatoryCategories),
            permissions);

        if (decision.IsDenied)
        {
            return decision.Reason;
        }

        if (!ComputerUseAutonomyPolicy.CanExecute(level))
        {
            return ComputerUseAutonomyPolicy.ExplainCannotExecute(level);
        }

        var available = _probe.GetAvailableMethods(request.TargetApp);
        if (!available.Contains(request.Method))
        {
            return $"No puedo usar {ComputerUseMethodText.Describe(request.Method)} en este equipo.";
        }

        // Diseño D19 — la comparación se hace entre métodos capaces del MISMO objetivo. Compararla
        // contra todos los disponibles rechazaba copiar al portapapeles "porque UI Automation es más
        // seguro", cuando UI Automation no sabe copiar. El orden sigue siendo estricto; lo que se
        // corrigió es el conjunto sobre el que se aplica.
        var goal = ComputerUseGoals.ForMethod(request.Method);
        var capable = goal is null ? available : ComputerUseGoals.CapableMethods(goal.Value);
        var candidates = available.Where(capable.Contains).ToArray();

        if (!ComputerUseMethodPolicy.IsSafestAvailable(request.Method, candidates, simulatedInputAllowed))
        {
            // Sin esto, el orden estricto de D17 sería decorativo.
            return $"Hay una forma más segura de {(goal is null ? "hacer esto" : ComputerUseGoals.Describe(goal.Value))} " +
                   $"que {ComputerUseMethodText.Describe(request.Method)}, así que no uso ésa.";
        }

        return null;
    }

    public ComputerUseResult Execute(
        ComputerUseRequest request,
        PermissionSettings permissions,
        AutonomyLevel level,
        bool simulatedInputAllowed = false)
    {
        var blocker = FindBlocker(request, permissions, level, simulatedInputAllowed);
        if (blocker is not null)
        {
            Record("Acción no ejecutada", $"{request.Description} — {blocker}", level, string.Empty, null);
            return ComputerUseResult.Refused(blocker);
        }

        return request.Method switch
        {
            ComputerUseMethod.UiAutomation => ExecuteUiAutomation(request, level),
            ComputerUseMethod.Portapapeles => ExecuteClipboard(request, level),
            ComputerUseMethod.ShellSeguro => ExecuteSafeCommand(request, level),
            _ => Refuse(
                request,
                level,
                $"Todavía no sé ejecutar por {ComputerUseMethodText.Describe(request.Method)}.")
        };
    }

    /// <summary>
    /// Diseño D19 — pulsar un control. **Sin snapshot y sin reversión**, y se dice: pulsar un botón
    /// ajeno no tiene vuelta atrás que Kohana pueda garantizar. Por eso el plan lo declara
    /// irreversible y por eso cada uno se confirma; prometer una reversión que no existe sería peor
    /// que no ofrecer la acción.
    /// </summary>
    private ComputerUseResult ExecuteUiAutomation(ComputerUseRequest request, AutonomyLevel level)
    {
        if (_invoker is null)
        {
            return Refuse(request, level, "No tengo forma de pulsar controles en este equipo.");
        }

        if (request.WindowHandle == 0 || string.IsNullOrWhiteSpace(request.ControlName))
        {
            return Refuse(request, level, "No me dijiste qué control pulsar ni en qué ventana.");
        }

        var step = _invoker.Invoke(request.WindowHandle, request.ControlName);
        if (!step.Success)
        {
            return Refuse(request, level, step.Detail);
        }

        Record("Control pulsado", $"{request.ControlName} — {request.Description}", level, string.Empty, null);

        return ComputerUseResult.Done(step.Detail, string.Empty, Guid.Empty);
    }

    private ComputerUseResult ExecuteClipboard(ComputerUseRequest request, AutonomyLevel level)
    {
        if (string.IsNullOrEmpty(request.ClipboardText))
        {
            return Refuse(request, level, "No me diste nada que copiar.");
        }

        // El portapapeles es el único método reversible de verdad hoy: se guarda lo que había.
        var snapshot = new ComputerUseSnapshot
        {
            At = _clock(),
            Description = request.Description,
            PreviousClipboard = _executor.ReadClipboard(),
            WrittenClipboard = request.ClipboardText
        };

        _snapshots.Save(snapshot);

        var step = _executor.SetClipboard(request.ClipboardText);
        if (!step.Success)
        {
            _snapshots.Remove(snapshot.Id);
            return Refuse(request, level, step.Detail);
        }

        // Verificar releyendo, igual que en las Fases 4 y 5.
        var actual = _executor.ReadClipboard();
        if (!string.Equals(actual, request.ClipboardText, StringComparison.Ordinal))
        {
            _snapshots.Remove(snapshot.Id);
            return Refuse(
                request,
                level,
                "Puse el texto en el portapapeles pero al releerlo había otra cosa, así que no lo doy por hecho.");
        }

        Record(
            "Portapapeles cambiado",
            request.Description,
            level,
            string.Empty,
            snapshot.Id,
            "Devolver al portapapeles lo que había antes.");

        return ComputerUseResult.Done(
            "Listo, lo tienes en el portapapeles. Pégalo donde quieras.", string.Empty, snapshot.Id);
    }

    private ComputerUseResult ExecuteSafeCommand(ComputerUseRequest request, AutonomyLevel level)
    {
        var command = SafeShellCatalog.Find(request.SafeCommandId);
        if (command is null)
        {
            // Nada compuesto fuera de la lista llega a ejecutarse, ni siquiera aquí dentro.
            return Refuse(request, level, "Ese comando no está en mi lista de permitidos, así que no lo ejecuto.");
        }

        var step = _executor.RunSafeCommand(command);
        if (!step.Success)
        {
            return Refuse(request, level, step.Detail);
        }

        // Sin snapshot a propósito: los comandos de la lista solo leen, así que no hay nada que
        // deshacer. Es una propiedad de la lista, y `SafeShellCatalog` la defiende.
        Record($"Comando ejecutado ({command.Id})", command.Title, level, step.Output, null);

        return ComputerUseResult.Done(step.Detail, step.Output, Guid.Empty);
    }

    /// <summary>
    /// Deshace un paso. **Se niega si el portapapeles cambió desde que Kohana lo puso**: devolverlo
    /// entonces borraría lo que la persona copió después. Misma regla que en D14 con los archivos, y
    /// por el mismo motivo.
    /// </summary>
    public ComputerUseResult Revert(Guid snapshotId, AutonomyLevel level)
    {
        var snapshot = _snapshots.Read().FirstOrDefault(entry => entry.Id == snapshotId);
        if (snapshot is null)
        {
            return ComputerUseResult.Refused("No tengo guardado ese paso, así que no puedo deshacerlo.");
        }

        var current = _executor.ReadClipboard();
        if (!string.Equals(current, snapshot.WrittenClipboard, StringComparison.Ordinal))
        {
            return ComputerUseResult.Refused(
                "El portapapeles cambió desde que lo puse. Si lo devolviera ahora perderías lo que " +
                "copiaste después, así que no lo toco.");
        }

        var step = _executor.SetClipboard(snapshot.PreviousClipboard ?? string.Empty);
        if (!step.Success)
        {
            return ComputerUseResult.Refused(step.Detail);
        }

        _snapshots.Remove(snapshot.Id);
        Record("Paso deshecho", snapshot.Description, level, string.Empty, null);

        return ComputerUseResult.Done("Devolví el portapapeles a como estaba.", string.Empty, snapshot.Id);
    }

    private ComputerUseResult Refuse(ComputerUseRequest request, AutonomyLevel level, string detail)
    {
        Record("Acción no ejecutada", $"{request.Description} — {detail}", level, string.Empty, null);
        return ComputerUseResult.Refused(detail);
    }

    private void Record(
        string action,
        string detail,
        AutonomyLevel level,
        string output,
        Guid? snapshotId,
        string revertHint = "")
    {
        var trimmedOutput = output.Length > 400 ? output[..400] + "…" : output;

        _audit.Append(new AuditEntry
        {
            At = _clock(),
            Capability = AuditCapability.Permisos,
            Action = action,
            Detail = string.IsNullOrWhiteSpace(trimmedOutput) ? detail : $"{detail} — {trimmedOutput}",
            Permission = "Acción confirmada por el usuario",
            AutonomyLevel = (int)level,
            RevertHint = revertHint,
            RevertToken = snapshotId?.ToString() ?? string.Empty
        });
    }
}
