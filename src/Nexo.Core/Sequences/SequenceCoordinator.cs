using Nexo.Core.Audit;
using Nexo.Core.Permissions;

namespace Nexo.Core.Sequences;

public enum SequenceOutcome
{
    /// <summary>Todos los pasos salieron bien.</summary>
    Completada,

    /// <summary>La persona dijo que no, antes de empezar o en un punto de riesgo.</summary>
    Cancelada,

    /// <summary>Un paso falló y se deshizo lo anterior.</summary>
    DetenidaYRevertida,

    /// <summary>Un paso falló y NO se pudo deshacer todo. Es el peor caso y se nombra aparte.</summary>
    DetenidaSinPoderRevertir,

    /// <summary>Ni siquiera empezó: permiso o nivel insuficiente.</summary>
    NoPermitida
}

public sealed record SequenceReport(
    SequenceOutcome Outcome,
    string Detail,
    int StepsCompleted,
    string? FailedStepId,
    IReadOnlyList<string> Log);

/// <summary>
/// Diseño D24 (nivel 5 — "Colaborar con confirmaciones") — ejecuta varios pasos seguidos.
///
/// Implementa las cuatro obligaciones que el modelo de confianza pone para la recuperación tras
/// fallo, y están numeradas en el código porque son la razón de que exista esta clase:
///
/// 1. **Detener la secuencia.** Al primer fallo se para. No se sigue "a ver si los demás van".
/// 2. **Dejar constancia del punto exacto de fallo** en el Audit Log: qué paso, el número que
///    ocupaba y cuántos se habían aplicado.
/// 3. **Ofrecer revertir lo ya aplicado**, en orden inverso.
/// 4. **Nunca reintentar automáticamente.** No hay reintentos aquí, ni con espera ni sin ella. Si
///    algo falló, lo decide la persona otra vez.
///
/// Y la que da nombre al nivel: **los puntos de riesgo se confirman uno a uno**, aunque la secuencia
/// entera ya esté aprobada. Aprobar "haz estas cinco cosas" no es aprobar la tercera si la tercera
/// borra algo.
/// </summary>
public sealed class SequenceCoordinator
{
    private readonly IAuditLog _audit;
    private readonly Func<DateTimeOffset> _clock;

    public SequenceCoordinator(IAuditLog audit, Func<DateTimeOffset>? clock = null)
    {
        _audit = audit;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    /// <summary>
    /// <paramref name="confirmRiskPoint"/> se llama en cada paso marcado como riesgo y devuelve si
    /// se sigue. Es un callback y no un booleano porque la respuesta tiene que pedirse **en ese
    /// momento**: preguntarlo todo al principio sería aprobar por adelantado, que es el nivel 6.
    /// </summary>
    public SequenceReport Run(
        SequencePlan plan,
        AutonomyLevel level,
        Func<SequenceStep, bool> confirmRiskPoint,
        Func<IReadOnlyList<SequenceStep>, bool>? confirmRollback = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(confirmRiskPoint);

        var log = new List<string>();

        if (!SequenceAutonomyPolicy.CanRunSequences(level))
        {
            const string detail =
                "Todavía no encadeno varios pasos seguidos. Súbelo a «Colaborar con confirmaciones» " +
                "si quieres que lo haga.";

            Record("Secuencia no permitida", $"{plan.Description} — {detail}", level, null);
            return new SequenceReport(SequenceOutcome.NoPermitida, detail, 0, null, log);
        }

        if (plan.Steps.Count == 0)
        {
            return new SequenceReport(SequenceOutcome.Completada, "No había nada que hacer.", 0, null, log);
        }

        var applied = new List<SequenceStep>();

        for (var index = 0; index < plan.Steps.Count; index++)
        {
            var step = plan.Steps[index];

            // El punto de riesgo se pregunta AQUÍ, no antes.
            if (step.IsRiskPoint && !confirmRiskPoint(step))
            {
                var detail = $"Paraste en el paso {index + 1} de {plan.Steps.Count}: {step.Title}.";
                log.Add(detail);

                Record("Secuencia cancelada", $"{plan.Description} — {detail}", level, step.Id);
                return new SequenceReport(SequenceOutcome.Cancelada, detail, applied.Count, null, log);
            }

            var result = step.Execute();
            log.Add($"{index + 1}. {step.Title}: {result.Detail}");

            if (result.Success)
            {
                applied.Add(step);
                continue;
            }

            // Obligación 1: detener. Obligación 2: constancia del punto exacto.
            var failure =
                $"Falló el paso {index + 1} de {plan.Steps.Count} ({step.Title}): {result.Detail}. " +
                $"Se habían aplicado {applied.Count}.";

            Record("Secuencia detenida", $"{plan.Description} — {failure}", level, step.Id);

            // Obligación 3: ofrecer revertir. Se OFRECE: revertir sin preguntar también sería
            // decidir por la persona, solo que hacia el otro lado.
            if (applied.Count == 0)
            {
                return new SequenceReport(
                    SequenceOutcome.DetenidaYRevertida,
                    $"{failure} No había nada aplicado que deshacer.",
                    0,
                    step.Id,
                    log);
            }

            if (confirmRollback is not null && !confirmRollback(applied))
            {
                return new SequenceReport(
                    SequenceOutcome.DetenidaSinPoderRevertir,
                    $"{failure} Dejaste los {applied.Count} pasos anteriores aplicados.",
                    applied.Count,
                    step.Id,
                    log);
            }

            var rolledBack = RollBack(applied, log, level, plan.Description);

            return new SequenceReport(
                rolledBack ? SequenceOutcome.DetenidaYRevertida : SequenceOutcome.DetenidaSinPoderRevertir,
                rolledBack
                    ? $"{failure} Deshice lo anterior: quedó como estaba."
                    : $"{failure} Y no pude deshacerlo todo. Revísalo antes de seguir.",
                applied.Count,
                step.Id,
                log);
        }

        Record("Secuencia completada", $"{plan.Description} — {plan.Steps.Count} pasos.", level, null);

        return new SequenceReport(
            SequenceOutcome.Completada,
            $"Hecho: {plan.Steps.Count} pasos.",
            plan.Steps.Count,
            null,
            log);
    }

    /// <summary>Se deshace en orden inverso: el último aplicado es el primero en volver atrás.</summary>
    private bool RollBack(
        IReadOnlyList<SequenceStep> applied,
        List<string> log,
        AutonomyLevel level,
        string description)
    {
        var allBack = true;

        foreach (var step in applied.Reverse())
        {
            if (!step.CanRevert || step.Revert is null)
            {
                log.Add($"No pude deshacer: {step.Title}.");
                allBack = false;
                continue;
            }

            var result = step.Revert();
            log.Add($"Deshecho: {step.Title} — {result.Detail}");
            allBack &= result.Success;
        }

        Record(
            allBack ? "Secuencia revertida" : "Secuencia revertida a medias",
            $"{description} — {applied.Count} pasos.",
            level,
            null);

        return allBack;
    }

    private void Record(string action, string detail, AutonomyLevel level, string? failedStepId) =>
        _audit.Append(new AuditEntry
        {
            At = _clock(),
            Capability = AuditCapability.Permisos,
            Action = action,
            Detail = failedStepId is null ? detail : $"{detail} [paso: {failedStepId}]",
            Permission = "Secuencia confirmada por el usuario",
            AutonomyLevel = (int)level
        });
}

/// <summary>
/// Diseño D24 — quién puede encadenar pasos.
///
/// El nivel 5 se abre **solo donde la reversión existe de verdad**. Para el acompañante de proyecto
/// sí: cada paso es una edición con su copia previa por archivo, así que "revertir lo ya aplicado"
/// se puede cumplir. Para Computer Use **no**, y no por prudencia genérica: de sus métodos
/// implementados solo el portapapeles sabe volver atrás, así que una secuencia que mezclara pulsar
/// controles con cualquier otra cosa no podría honrar la obligación 3 del modelo de confianza. Abrir
/// el nivel ahí sería prometer una recuperación que no existe.
///
/// El nivel 6 sigue cerrado para todo.
/// </summary>
public static class SequenceAutonomyPolicy
{
    public static bool CanRunSequences(AutonomyLevel level) =>
        level == AutonomyLevel.ColaborarConConfirmaciones;

    public static string ExplainForComputerUse() =>
        "En el equipo hago una acción cada vez. Encadenar varias exigiría poder deshacerlas todas, " +
        "y de las formas que tengo de actuar solo el portapapeles sabe volver atrás.";
}
