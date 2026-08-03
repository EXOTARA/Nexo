using Nexo.Core.Audit;
using Nexo.Core.Permissions;
using Nexo.Core.Sequences;
using Nexo.Core.Workspace;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D24 (nivel 5) — el modelo de confianza pone cuatro obligaciones para la recuperación tras
/// fallo: detener la secuencia, dejar constancia del punto exacto, ofrecer revertir lo ya aplicado y
/// **nunca reintentar automáticamente**. Cada una tiene su prueba aquí.
/// </summary>
public sealed class SequenceCoordinatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 2, 18, 0, 0, TimeSpan.FromHours(-6));

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public IReadOnlyList<AuditEntry> Read() => Entries;

        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class Recorder
    {
        public List<string> Executed { get; } = [];

        public List<string> Reverted { get; } = [];

        public HashSet<string> Failing { get; } = [];

        public int ExecuteCalls { get; private set; }

        public SequenceStep Step(string id, bool riskPoint = false, bool canRevert = true) => new(
            id,
            $"Paso {id}",
            riskPoint,
            canRevert,
            Execute: () =>
            {
                ExecuteCalls++;

                if (Failing.Contains(id))
                {
                    return SequenceStepResult.Failed("algo falló");
                }

                Executed.Add(id);
                return SequenceStepResult.Ok("hecho");
            },
            Revert: canRevert
                ? () =>
                {
                    Reverted.Add(id);
                    return SequenceStepResult.Ok("deshecho");
                }
                : null);
    }

    private static (SequenceCoordinator Coordinator, FakeAuditLog Audit) Build()
    {
        var audit = new FakeAuditLog();
        return (new SequenceCoordinator(audit, () => Now), audit);
    }

    private static SequencePlan Plan(params SequenceStep[] steps) => new("Secuencia de prueba", steps);

    // ---------- El nivel manda ----------

    [Theory]
    [InlineData(AutonomyLevel.Ver)]
    [InlineData(AutonomyLevel.Proponer)]
    [InlineData(AutonomyLevel.EjecutarUnPaso)]
    public void BelowLevelFive_NoSequenceRuns(AutonomyLevel level)
    {
        var (coordinator, _) = Build();
        var recorder = new Recorder();

        var report = coordinator.Run(Plan(recorder.Step("a")), level, _ => true, _ => true);

        Assert.Equal(SequenceOutcome.NoPermitida, report.Outcome);
        Assert.Empty(recorder.Executed);
    }

    [Fact]
    public void LevelSixDoesNotUnlockSequencesEither() =>
        // Sigue cerrado: automatizar sin preguntar por el camino es otro problema.
        Assert.False(SequenceAutonomyPolicy.CanRunSequences(AutonomyLevel.AutomatizarSecuencia));

    // ---------- Puntos de riesgo ----------

    [Fact]
    public void EachRiskPointIsAskedSeparately()
    {
        // Aprobar "haz estas cinco cosas" no es aprobar la tercera si la tercera borra algo.
        var (coordinator, _) = Build();
        var recorder = new Recorder();
        var asked = new List<string>();

        coordinator.Run(
            Plan(recorder.Step("a", riskPoint: true), recorder.Step("b", riskPoint: true)),
            AutonomyLevel.ColaborarConConfirmaciones,
            step =>
            {
                asked.Add(step.Id);
                return true;
            },
            _ => true);

        Assert.Equal(["a", "b"], asked);
    }

    [Fact]
    public void SayingNoAtARiskPoint_StopsThere()
    {
        var (coordinator, _) = Build();
        var recorder = new Recorder();

        var report = coordinator.Run(
            Plan(recorder.Step("a"), recorder.Step("b", riskPoint: true), recorder.Step("c")),
            AutonomyLevel.ColaborarConConfirmaciones,
            _ => false,
            _ => true);

        Assert.Equal(SequenceOutcome.Cancelada, report.Outcome);
        Assert.Equal(["a"], recorder.Executed);
    }

    [Fact]
    public void StepsThatAreNotRiskPoints_AreNotAsked()
    {
        var (coordinator, _) = Build();
        var recorder = new Recorder();
        var asked = 0;

        coordinator.Run(
            Plan(recorder.Step("a"), recorder.Step("b")),
            AutonomyLevel.ColaborarConConfirmaciones,
            _ => { asked++; return true; },
            _ => true);

        Assert.Equal(0, asked);
    }

    // ---------- Las cuatro obligaciones ----------

    [Fact]
    public void ObligationOne_TheSequenceStopsAtTheFirstFailure()
    {
        var (coordinator, _) = Build();
        var recorder = new Recorder();
        recorder.Failing.Add("b");
        var rollbackWasAsked = false;

        coordinator.Run(
            Plan(recorder.Step("a"), recorder.Step("b"), recorder.Step("c")),
            AutonomyLevel.ColaborarConConfirmaciones,
            _ => true,
            applied =>
            {
                // Corrección de un defecto real: este parámetro tenía "= null" por omisión, y con
                // null el código revertía "a" AQUÍ MISMO sin preguntar — justo cuando ya se sabe
                // que algo se aplicó y algo falló después, que es el caso exacto que esta prueba
                // ejercita. Que ahora sea obligatorio (sin valor por omisión) hace que ese olvido
                // sea un error de compilación; esta aserción prueba además que, cuando SÍ se pasa,
                // de verdad se invoca antes de tocar nada.
                rollbackWasAsked = true;
                return true;
            });

        Assert.True(rollbackWasAsked);

        // "c" no llegó a intentarse: no se sigue "a ver si los demás van".
        Assert.Equal(["a"], recorder.Executed);
        Assert.DoesNotContain("c", recorder.Reverted);
    }

    [Fact]
    public void ObligationTwo_TheExactFailurePointIsRecorded()
    {
        var (coordinator, audit) = Build();
        var recorder = new Recorder();
        recorder.Failing.Add("b");

        var report = coordinator.Run(
            Plan(recorder.Step("a"), recorder.Step("b"), recorder.Step("c")),
            AutonomyLevel.ColaborarConConfirmaciones,
            _ => true,
            _ => true);

        Assert.Equal("b", report.FailedStepId);
        Assert.Contains("Falló el paso 2 de 3", report.Detail);

        var entry = audit.Entries.First(entry => entry.Action == "Secuencia detenida");
        Assert.Contains("[paso: b]", entry.Detail);
    }

    [Fact]
    public void ObligationThree_WhatWasAppliedIsOfferedForRollback()
    {
        var (coordinator, _) = Build();
        var recorder = new Recorder();
        recorder.Failing.Add("c");
        var offered = 0;

        var report = coordinator.Run(
            Plan(recorder.Step("a"), recorder.Step("b"), recorder.Step("c")),
            AutonomyLevel.ColaborarConConfirmaciones,
            _ => true,
            applied => { offered = applied.Count; return true; });

        Assert.Equal(2, offered);
        Assert.Equal(SequenceOutcome.DetenidaYRevertida, report.Outcome);

        // En orden inverso: el último aplicado vuelve primero.
        Assert.Equal(["b", "a"], recorder.Reverted);
    }

    [Fact]
    public void RollbackIsOffered_NotImposed()
    {
        // Revertir sin preguntar también sería decidir por la persona, solo que hacia el otro lado.
        var (coordinator, _) = Build();
        var recorder = new Recorder();
        recorder.Failing.Add("b");

        var report = coordinator.Run(
            Plan(recorder.Step("a"), recorder.Step("b")),
            AutonomyLevel.ColaborarConConfirmaciones,
            _ => true,
            _ => false);

        Assert.Empty(recorder.Reverted);
        Assert.Equal(SequenceOutcome.DetenidaSinPoderRevertir, report.Outcome);
        Assert.Contains("Dejaste los 1 pasos anteriores aplicados", report.Detail);
    }

    [Fact]
    public void ObligationFour_NothingIsRetriedAutomatically()
    {
        var (coordinator, _) = Build();
        var recorder = new Recorder();
        recorder.Failing.Add("a");

        coordinator.Run(
            Plan(recorder.Step("a")), AutonomyLevel.ColaborarConConfirmaciones, _ => true, _ => true);

        // Un solo intento. Ni con espera ni sin ella.
        Assert.Equal(1, recorder.ExecuteCalls);
    }

    // ---------- Reversión parcial ----------

    [Fact]
    public void AStepThatCannotRevert_IsSaidBeforeStarting()
    {
        var recorder = new Recorder();
        var plan = Plan(recorder.Step("a", canRevert: false), recorder.Step("b"));

        Assert.False(plan.IsFullyReversible);
        Assert.Contains("no puedo deshacer", plan.ReversalWarning);
    }

    [Fact]
    public void WhenSomethingCouldNotBeReverted_ItIsNotClaimedFine()
    {
        var (coordinator, _) = Build();
        var recorder = new Recorder();
        recorder.Failing.Add("c");

        var report = coordinator.Run(
            Plan(recorder.Step("a", canRevert: false), recorder.Step("b"), recorder.Step("c")),
            AutonomyLevel.ColaborarConConfirmaciones,
            _ => true,
            _ => true);

        Assert.Equal(SequenceOutcome.DetenidaSinPoderRevertir, report.Outcome);
        Assert.Contains("Revísalo", report.Detail);
    }

    [Fact]
    public void WithNothingAppliedYet_ThereIsNothingToRollBack()
    {
        var (coordinator, _) = Build();
        var recorder = new Recorder();
        recorder.Failing.Add("a");

        var report = coordinator.Run(
            Plan(recorder.Step("a")), AutonomyLevel.ColaborarConConfirmaciones, _ => true, _ => true);

        Assert.Equal(SequenceOutcome.DetenidaYRevertida, report.Outcome);
        Assert.Contains("No había nada aplicado", report.Detail);
    }

    // ---------- Camino feliz ----------

    [Fact]
    public void ASequenceThatWorks_RunsEveryStepInOrder()
    {
        var (coordinator, audit) = Build();
        var recorder = new Recorder();

        var report = coordinator.Run(
            Plan(recorder.Step("a"), recorder.Step("b"), recorder.Step("c")),
            AutonomyLevel.ColaborarConConfirmaciones,
            _ => true,
            _ => true);

        Assert.Equal(SequenceOutcome.Completada, report.Outcome);
        Assert.Equal(["a", "b", "c"], recorder.Executed);
        Assert.Contains(audit.Entries, entry => entry.Action == "Secuencia completada");
    }

    // ---------- Dónde se abre el nivel 5 ----------

    [Fact]
    public void TheProjectCanChainSteps_BecauseEveryStepHasACheckpoint() =>
        Assert.True(WorkspaceAutonomyPolicy.IsAvailable(AutonomyLevel.ColaborarConConfirmaciones));

    [Fact]
    public void ComputerUseCannotChainSteps_BecauseItsMethodsCannotAllBeUndone()
    {
        // No es prudencia genérica: de los métodos implementados solo el portapapeles sabe volver
        // atrás, así que la obligación 3 no se podría cumplir.
        Assert.False(ComputerUse.ComputerUseAutonomyPolicy.IsAvailable(
            AutonomyLevel.ColaborarConConfirmaciones));

        Assert.Contains("portapapeles", SequenceAutonomyPolicy.ExplainForComputerUse());
    }

    [Fact]
    public void LevelSixIsClosedEverywhere()
    {
        Assert.False(WorkspaceAutonomyPolicy.IsAvailable(AutonomyLevel.AutomatizarSecuencia));
        Assert.False(ComputerUse.ComputerUseAutonomyPolicy.IsAvailable(AutonomyLevel.AutomatizarSecuencia));
    }
}
