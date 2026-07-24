using Nexo.Core.Automation;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Fase 1.1.1 — corrige el defecto D2: <c>OpenApplication</c> reenviaba
/// <see cref="AutomationAction.Arguments"/> al proceso mientras estaba clasificada como
/// <see cref="AutomationRiskLevel.Reversible"/>, es decir, sin confirmación.
///
/// Cubre el escenario 22 de `TEST_MATRIX.md` (prueba de seguridad, bloqueante de RC):
/// **abrir una terminal no requiere confirmación; ejecutar un comando dentro de ella sí.**
///
/// Ninguna prueba ejecuta un proceso real: todas evalúan la política tipada.
/// </summary>
public sealed class ShellExecutionPolicyTests
{
    private static AutomationRiskLevel RiskOfOpening(string target, string arguments = "") =>
        AutomationPermissionPolicy.GetRisk(new AutomationAction
        {
            Type = AutomationActionType.OpenApplication,
            Target = target,
            Arguments = arguments
        });

    // ---------- 1. OpenTerminal sin argumentos ----------

    [Fact]
    public void OpenTerminalWithoutArguments_IsAllowed()
    {
        var action = new AutomationAction
        {
            Type = AutomationActionType.OpenTerminal,
            WorkingDirectory = @"C:\Dev"
        };

        Assert.True(AutomationPermissionPolicy.IsAllowed(action, out _));

        // Sigue siendo Sensitive en la ruta de rutinas, como ya ocurría antes de 1.1.1.
        // El ejecutor ignora `Arguments` para este tipo, así que no puede convertirse en
        // ejecución arbitraria.
        Assert.Equal(AutomationRiskLevel.Sensitive, AutomationPermissionPolicy.GetRisk(action));
    }

    // ---------- 2. powershell.exe sin argumentos ----------

    [Theory]
    [InlineData("powershell.exe")]
    [InlineData("powershell")]
    [InlineData("pwsh.exe")]
    [InlineData("cmd.exe")]
    public void AnInterpreterWithoutArguments_IsJustOpeningIt(string target)
    {
        // Abrir la terminal no es ejecutar en ella: decisión F de PRODUCT_VISION.
        Assert.Equal(AutomationRiskLevel.Reversible, RiskOfOpening(target));
    }

    // ---------- 3, 4, 5, 6. Intérpretes con argumentos ----------

    [Theory]
    [InlineData("powershell.exe", "-Command \"Get-Process\"")]
    [InlineData("powershell.exe", "-EncodedCommand SQBuAHYAbwBrAGUA")]
    [InlineData("powershell.exe", "-File script.ps1")]
    [InlineData("pwsh.exe", "-File script.ps1")]
    [InlineData("pwsh", "-c \"ls\"")]
    [InlineData("cmd.exe", "/c dir")]
    [InlineData("cmd", "/k whoami")]
    [InlineData("wscript.exe", "script.vbs")]
    [InlineData("cscript.exe", "//nologo script.vbs")]
    [InlineData("mshta.exe", "javascript:close()")]
    [InlineData("rundll32.exe", "shell32.dll,Control_RunDLL")]
    [InlineData("regsvr32.exe", "/s /u /i:http://ejemplo scrobj.dll")]
    [InlineData("wsl.exe", "-e ls")]
    [InlineData("bash.exe", "-c ls")]
    [InlineData("python.exe", "-c \"print(1)\"")]
    [InlineData("node.exe", "-e \"process.exit()\"")]
    public void AnInterpreterWithArguments_RequiresConfirmation(string target, string arguments)
    {
        Assert.Equal(AutomationRiskLevel.Sensitive, RiskOfOpening(target, arguments));
    }

    // ---------- No basarse solo en el nombre visible ----------

    [Theory]
    [InlineData(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe")]
    [InlineData(@"C:/Windows/System32/cmd.exe")]
    [InlineData("\"C:\\Windows\\System32\\cmd.exe\"")]
    [InlineData(@"%SystemRoot%\System32\cmd.exe")]
    [InlineData("POWERSHELL.EXE")]
    [InlineData("  powershell.exe  ")]
    [InlineData("powershell.exe.")]
    [InlineData("powershell.exe ")]
    public void InterpreterDetection_SurvivesPathsQuotesCaseAndTrailingCharacters(string target)
    {
        Assert.True(ShellExecutionPolicy.IsInterpreter(target));
        Assert.Equal(AutomationRiskLevel.Sensitive, RiskOfOpening(target, "-Command \"x\""));
    }

    [Fact]
    public void LaunchingAnInterpreterThroughAnotherProgram_AlsoRequiresConfirmation()
    {
        // Rodeo evidente: el objetivo parece inocuo y el intérprete viaja en los argumentos.
        Assert.Equal(
            AutomationRiskLevel.Sensitive,
            RiskOfOpening("explorer.exe", "powershell -Command \"Get-Process\""));

        Assert.Equal(
            AutomationRiskLevel.Sensitive,
            RiskOfOpening("cualquier.exe", @"C:\Windows\System32\cmd.exe /c dir"));
    }

    [Fact]
    public void QuotedInterpreterPathsInArguments_AreStillDetected()
    {
        Assert.True(ShellExecutionPolicy.MentionsInterpreter(
            "\"C:\\Windows\\System32\\cmd.exe\" /c dir"));
    }

    // ---------- 7. Aplicación normal con argumentos inocuos ----------

    [Theory]
    [InlineData("notepad.exe", "notas.txt")]
    [InlineData("code.exe", @"C:\Dev\Nexo")]
    [InlineData("chrome.exe", "https://example.com")]
    [InlineData("spotify.exe", "")]
    public void AnOrdinaryApplicationWithHarmlessArguments_StaysReversible(
        string target,
        string arguments)
    {
        // COMPORTAMIENTO DOCUMENTADO: abrir una aplicación normal con argumentos sigue sin
        // pedir confirmación. La protección se centra en los intérpretes, que son los que
        // convierten un argumento en ejecución de código arbitrario. Ampliar esto a cualquier
        // argumento haría inutilizable "abre VS Code en esta carpeta" sin ganar seguridad real.
        Assert.Equal(AutomationRiskLevel.Reversible, RiskOfOpening(target, arguments));
    }

    [Fact]
    public void AnOrdinaryApplicationIsNotAnInterpreter()
    {
        Assert.False(ShellExecutionPolicy.IsInterpreter("notepad.exe"));
        Assert.False(ShellExecutionPolicy.IsInterpreter(""));
        Assert.False(ShellExecutionPolicy.IsInterpreter(null));
    }

    [Fact]
    public void WhitespaceOnlyArguments_DoNotCountAsExecution()
    {
        Assert.Equal(AutomationRiskLevel.Reversible, RiskOfOpening("powershell.exe", "   "));
    }

    // ---------- 8. Una rutina no puede encapsular un comando arbitrario ----------

    [Fact]
    public void ARoutineWrappingAShellCommand_RequiresConfirmation()
    {
        var routine = new RoutineDefinition
        {
            Name = "Arranque",
            TriggerPhrase = "modo arranque",
            RequiresConfirmation = false,
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.OpenApplication,
                    Target = "powershell.exe",
                    Arguments = "-Command \"Get-Process\""
                }
            ]
        };

        // Antes de 1.1.1 esto devolvía false: la rutina se ejecutaba sin preguntar.
        Assert.True(AutomationPermissionPolicy.RequiresConfirmation(routine));
    }

    [Fact]
    public async Task ARoutineWithAShellCommand_DoesNotRunWithoutExplicitApproval()
    {
        var executor = new RecordingExecutor();
        var runner = new RoutineRunner(executor);
        var routine = ShellRoutine();

        var report = await runner.RunAsync(routine);

        // El permiso se aplica en el ejecutor, no se confía a que la interfaz preguntara.
        Assert.Empty(executor.Executed);
        Assert.Equal(1, report.FailedCount);
        Assert.Contains("Confirmación requerida", report.Results[0].Title);
    }

    [Fact]
    public async Task ARoutineWithAShellCommand_RunsOnlyAfterExplicitApproval()
    {
        var executor = new RecordingExecutor();
        var runner = new RoutineRunner(executor);

        var report = await runner.RunAsync(
            ShellRoutine(),
            RoutineExecutionApproval.ConfirmedByUser);

        Assert.Single(executor.Executed);
        Assert.Equal(1, report.SucceededCount);
    }

    [Fact]
    public async Task ApprovalIsPerExecution_NotStoredOnTheRoutine()
    {
        // Una rutina aprobada una vez no hereda permiso: la siguiente ejecución sin
        // aprobación vuelve a rechazarse.
        var executor = new RecordingExecutor();
        var runner = new RoutineRunner(executor);
        var routine = ShellRoutine();

        await runner.RunAsync(routine, RoutineExecutionApproval.ConfirmedByUser);
        Assert.Single(executor.Executed);

        await runner.RunAsync(routine);

        Assert.Single(executor.Executed);
    }

    [Fact]
    public async Task HarmlessStepsStillRunWithoutApproval()
    {
        // La protección no debe romper las rutinas normales.
        var executor = new RecordingExecutor();
        var runner = new RoutineRunner(executor);
        var routine = new RoutineDefinition
        {
            Name = "Enfoque",
            TriggerPhrase = "modo enfoque",
            Steps =
            [
                new AutomationAction { Type = AutomationActionType.CreateTask, Text = "leer" },
                new AutomationAction { Type = AutomationActionType.StartFocus, NumericValue = 25 }
            ]
        };

        var report = await runner.RunAsync(routine);

        Assert.Equal(2, executor.Executed.Count);
        Assert.Equal(2, report.SucceededCount);
    }

    [Fact]
    public async Task ASensitiveStepIsSkippedButTheRestOfTheRoutineContinues()
    {
        var executor = new RecordingExecutor();
        var runner = new RoutineRunner(executor);
        var routine = new RoutineDefinition
        {
            Name = "Mixta",
            TriggerPhrase = "modo mixto",
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.OpenApplication,
                    Target = "cmd.exe",
                    Arguments = "/c dir"
                },
                new AutomationAction { Type = AutomationActionType.CreateTask, Text = "leer" }
            ]
        };

        var report = await runner.RunAsync(routine);

        Assert.Single(executor.Executed);
        Assert.Equal(AutomationActionType.CreateTask, executor.Executed[0].Type);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(1, report.SucceededCount);
    }

    private static RoutineDefinition ShellRoutine() => new()
    {
        Name = "Arranque",
        TriggerPhrase = "modo arranque",
        RequiresConfirmation = false,
        Steps =
        [
            new AutomationAction
            {
                Type = AutomationActionType.OpenApplication,
                Target = "powershell.exe",
                Arguments = "-Command \"Get-Process\""
            }
        ]
    };

    /// <summary>
    /// Ejecutor falso: registra lo que se le pide y nunca lanza un proceso real.
    /// </summary>
    private sealed class RecordingExecutor : IAutomationActionExecutor
    {
        public List<AutomationAction> Executed { get; } = [];

        public Task<AutomationActionResult> ExecuteAsync(
            AutomationAction action,
            CancellationToken cancellationToken)
        {
            Executed.Add(action.Copy());
            return Task.FromResult(
                AutomationActionResult.Completed(action, "Listo", "Acción simulada"));
        }
    }
}
