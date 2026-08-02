using Nexo.Core.Audit;
using Nexo.Core.ComputerUse;
using Nexo.Core.Permissions;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D18 (Fase 7, nivel 4) — la primera vez que Kohana actúa sobre el equipo. Es la capacidad
/// más arriesgada del roadmap, así que lo que se prueba aquí es cuándo se NIEGA.
/// </summary>
public sealed class ComputerUseCoordinatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 2, 10, 0, 0, TimeSpan.FromHours(-6));

    // ---------- Dobles ----------

    private sealed class FakeExecutor : IComputerUseExecutor
    {
        public string? Clipboard { get; set; } = "lo que había antes";

        public bool ClipboardUnreadable { get; set; }

        public bool FailClipboard { get; set; }

        public string? WriteInsteadOf { get; set; }

        public List<string> CommandsRun { get; } = [];

        public bool FailCommand { get; set; }

        public string? ReadClipboard() => ClipboardUnreadable ? null : Clipboard;

        public ComputerUseStepResult SetClipboard(string text)
        {
            if (FailClipboard)
            {
                return ComputerUseStepResult.Failed("El portapapeles está ocupado.");
            }

            Clipboard = WriteInsteadOf ?? text;
            return ComputerUseStepResult.Ok("Copiado.");
        }

        public ComputerUseStepResult RunSafeCommand(SafeShellCommand command)
        {
            if (FailCommand)
            {
                return ComputerUseStepResult.Failed("No pude ejecutarlo.");
            }

            CommandsRun.Add(command.Id);
            return ComputerUseStepResult.Ok($"Listo: {command.Title}.", "salida del comando");
        }
    }

    private sealed class FakeProbe(params ComputerUseMethod[] methods) : IComputerUseMethodProbe
    {
        public IReadOnlyList<ComputerUseMethod> GetAvailableMethods(string? targetApp) => methods;
    }

    private sealed class FakeSnapshotStore : IComputerUseSnapshotStore
    {
        public List<ComputerUseSnapshot> Saved { get; } = [];

        public IReadOnlyList<ComputerUseSnapshot> Read() =>
            Saved.Select(snapshot => snapshot.Copy()).ToArray();

        public void Save(ComputerUseSnapshot snapshot) => Saved.Add(snapshot.Copy());

        public void Remove(Guid snapshotId) => Saved.RemoveAll(snapshot => snapshot.Id == snapshotId);
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public IReadOnlyList<AuditEntry> Read() => Entries;

        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    // ---------- Ayudas ----------

    private static PermissionSettings Permissions(PermissionLevel level = PermissionLevel.Permitido)
    {
        var settings = new PermissionSettings();
        settings.Normalize();
        settings.For(KohanaCapability.ComputerUse).Level = level;
        return settings;
    }

    private static (ComputerUseCoordinator Coordinator,
        FakeExecutor Executor,
        FakeSnapshotStore Snapshots,
        FakeAuditLog Audit) Build(params ComputerUseMethod[] available)
    {
        var executor = new FakeExecutor();
        var snapshots = new FakeSnapshotStore();
        var audit = new FakeAuditLog();
        var probe = new FakeProbe(available.Length == 0
            ? [ComputerUseMethod.ShellSeguro, ComputerUseMethod.Portapapeles]
            : available);

        return (new ComputerUseCoordinator(executor, probe, snapshots, audit, () => Now),
            executor, snapshots, audit);
    }

    private static ComputerUseRequest Clipboard(string text = "texto nuevo") =>
        new(ComputerUseMethod.Portapapeles, "Copiar un texto", ClipboardText: text);

    private static ComputerUseRequest Command(string id = "net.ip") =>
        new(ComputerUseMethod.ShellSeguro, "Ver la red", SafeCommandId: id);

    // ---------- Permisos y nivel ----------

    [Fact]
    public void WithComputerUseBlocked_NothingHappens()
    {
        var (coordinator, executor, _, _) = Build();

        var result = coordinator.Execute(
            Command(), Permissions(PermissionLevel.Bloqueado), AutonomyLevel.EjecutarUnPaso);

        Assert.False(result.Success);
        Assert.Empty(executor.CommandsRun);
    }

    [Theory]
    [InlineData(AutonomyLevel.Ver)]
    [InlineData(AutonomyLevel.Guiar)]
    [InlineData(AutonomyLevel.Proponer)]
    public void BelowLevelFour_NothingIsExecuted(AutonomyLevel level)
    {
        var (coordinator, executor, _, _) = Build();

        var result = coordinator.Execute(Command(), Permissions(), level);

        Assert.False(result.Success);
        Assert.Empty(executor.CommandsRun);
    }

    // ---------- El orden estricto no es decorativo ----------

    [Fact]
    public void AskingForALessSafeMethod_WhenABetterOneExists_IsRefused()
    {
        // Sin esto bastaría con pedir el método cómodo para saltarse el orden de D17.
        var (coordinator, executor, _, _) = Build(
            ComputerUseMethod.ShellSeguro, ComputerUseMethod.Portapapeles);

        var result = coordinator.Execute(Clipboard(), Permissions(), AutonomyLevel.EjecutarUnPaso);

        Assert.False(result.Success);
        Assert.Contains("más segura", result.Detail);
        Assert.Equal("lo que había antes", executor.Clipboard);
    }

    [Fact]
    public void AMethodNotAvailableOnThisMachine_IsRefused()
    {
        var (coordinator, _, _, _) = Build(ComputerUseMethod.Portapapeles);

        var result = coordinator.Execute(Command(), Permissions(), AutonomyLevel.EjecutarUnPaso);

        Assert.False(result.Success);
        Assert.Contains("No puedo usar", result.Detail);
    }

    [Fact]
    public void AMethodKohanaCannotExecuteYet_IsRefused()
    {
        var (coordinator, _, _, _) = Build(ComputerUseMethod.UiAutomation);

        var result = coordinator.Execute(
            new ComputerUseRequest(ComputerUseMethod.UiAutomation, "Pulsar un botón"),
            Permissions(),
            AutonomyLevel.EjecutarUnPaso);

        Assert.False(result.Success);
        Assert.Contains("Todavía no sé ejecutar", result.Detail);
    }

    // ---------- Portapapeles ----------

    [Fact]
    public void TheClipboard_IsSetAndAudited_WithAWayBack()
    {
        var (coordinator, executor, snapshots, audit) = Build(ComputerUseMethod.Portapapeles);

        var result = coordinator.Execute(Clipboard(), Permissions(), AutonomyLevel.EjecutarUnPaso);

        Assert.True(result.Success);
        Assert.Equal("texto nuevo", executor.Clipboard);
        Assert.Equal("lo que había antes", Assert.Single(snapshots.Saved).PreviousClipboard);

        var entry = audit.Entries.Last();
        Assert.True(entry.CanRevert);
        Assert.Equal(4, entry.AutonomyLevel);
    }

    [Fact]
    public void IfTheClipboardDidNotEndUpAsAsked_ItIsNotClaimedDone()
    {
        var snapshots = new FakeSnapshotStore();
        var coordinator = new ComputerUseCoordinator(
            new FakeExecutor { WriteInsteadOf = "otra cosa" },
            new FakeProbe(ComputerUseMethod.Portapapeles),
            snapshots,
            new FakeAuditLog(),
            () => Now);

        var result = coordinator.Execute(Clipboard(), Permissions(), AutonomyLevel.EjecutarUnPaso);

        Assert.False(result.Success);
        Assert.Contains("al releerlo había otra cosa", result.Detail);
        Assert.Empty(snapshots.Saved);
    }

    [Fact]
    public void IfTheClipboardCannotBeSet_NoSnapshotIsLeftBehind()
    {
        var snapshots = new FakeSnapshotStore();
        var coordinator = new ComputerUseCoordinator(
            new FakeExecutor { FailClipboard = true },
            new FakeProbe(ComputerUseMethod.Portapapeles),
            snapshots,
            new FakeAuditLog(),
            () => Now);

        Assert.False(coordinator.Execute(Clipboard(), Permissions(), AutonomyLevel.EjecutarUnPaso).Success);
        Assert.Empty(snapshots.Saved);
    }

    [Fact]
    public void EmptyClipboardText_IsRefused()
    {
        var (coordinator, _, _, _) = Build(ComputerUseMethod.Portapapeles);

        Assert.False(coordinator.Execute(
            Clipboard(string.Empty), Permissions(), AutonomyLevel.EjecutarUnPaso).Success);
    }

    // ---------- Deshacer ----------

    [Fact]
    public void Undo_ReturnsTheClipboardToWhatItWas()
    {
        var (coordinator, executor, _, _) = Build(ComputerUseMethod.Portapapeles);

        var done = coordinator.Execute(Clipboard(), Permissions(), AutonomyLevel.EjecutarUnPaso);
        var result = coordinator.Revert(done.SnapshotId, AutonomyLevel.EjecutarUnPaso);

        Assert.True(result.Success);
        Assert.Equal("lo que había antes", executor.Clipboard);
    }

    [Fact]
    public void UndoRefuses_IfTheClipboardChangedAfterKohanaSetIt()
    {
        // Misma regla que con los archivos en D14: deshacer no puede destruir lo que hizo la
        // persona después.
        var (coordinator, executor, _, _) = Build(ComputerUseMethod.Portapapeles);

        var done = coordinator.Execute(Clipboard(), Permissions(), AutonomyLevel.EjecutarUnPaso);
        executor.Clipboard = "algo que copié yo luego";

        var result = coordinator.Revert(done.SnapshotId, AutonomyLevel.EjecutarUnPaso);

        Assert.False(result.Success);
        Assert.Contains("perderías lo que copiaste", result.Detail);
        Assert.Equal("algo que copié yo luego", executor.Clipboard);
    }

    [Fact]
    public void UndoingSomethingUnknown_IsRefused()
    {
        var (coordinator, _, _, _) = Build();

        Assert.False(coordinator.Revert(Guid.NewGuid(), AutonomyLevel.EjecutarUnPaso).Success);
    }

    // ---------- Comandos de la lista ----------

    [Fact]
    public void AnAllowedCommand_RunsAndIsAudited()
    {
        var (coordinator, executor, snapshots, audit) = Build(ComputerUseMethod.ShellSeguro);

        var result = coordinator.Execute(Command(), Permissions(), AutonomyLevel.EjecutarUnPaso);

        Assert.True(result.Success);
        Assert.Equal(["net.ip"], executor.CommandsRun);
        Assert.Contains("salida del comando", result.Output);

        // Sin snapshot: los comandos de la lista solo leen, así que no hay nada que deshacer.
        Assert.Empty(snapshots.Saved);
        Assert.False(audit.Entries.Last().CanRevert);
    }

    [Fact]
    public void ACommandOutsideTheAllowList_NeverRuns()
    {
        var (coordinator, executor, _, _) = Build(ComputerUseMethod.ShellSeguro);

        var result = coordinator.Execute(
            Command("rm -rf /"), Permissions(), AutonomyLevel.EjecutarUnPaso);

        Assert.False(result.Success);
        Assert.Contains("lista de permitidos", result.Detail);
        Assert.Empty(executor.CommandsRun);
    }

    // ---------- Todo intento queda registrado ----------

    [Fact]
    public void ARefusedAttempt_IsAuditedToo()
    {
        var (coordinator, _, _, audit) = Build();

        coordinator.Execute(Command(), Permissions(PermissionLevel.Bloqueado), AutonomyLevel.EjecutarUnPaso);

        Assert.Contains(audit.Entries, entry => entry.Action == "Acción no ejecutada");
    }
}
