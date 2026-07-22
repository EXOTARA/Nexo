using System.Diagnostics;
using System.IO;
using Nexo.Core.Audio;
using Nexo.Core.Automation;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;
using NexoFocusManager = Nexo.Core.Focus.FocusManager;

namespace Nexo.App.Automation;

public sealed class NexoAutomationActionExecutor : IAutomationActionExecutor
{
    private readonly IAudioMixerService _audioService;
    private readonly NexoFocusManager _focusManager;
    private readonly TaskManager _taskManager;

    public NexoAutomationActionExecutor(
        IAudioMixerService audioService,
        NexoFocusManager focusManager,
        TaskManager taskManager)
    {
        _audioService = audioService;
        _focusManager = focusManager;
        _taskManager = taskManager;
    }

    public async Task<AutomationActionResult> ExecuteAsync(
        AutomationAction action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return action.Type switch
        {
            AutomationActionType.OpenApplication => OpenApplication(action),
            AutomationActionType.OpenFolder => OpenFolder(action),
            AutomationActionType.OpenTerminal => OpenTerminal(action),
            AutomationActionType.SetApplicationVolume => await RunAudioAsync(
                action,
                () => _audioService.SetApplicationVolume(action.Target, action.NumericValue ?? 0),
                cancellationToken),
            AutomationActionType.MuteApplication => await RunAudioAsync(
                action,
                () => _audioService.SetApplicationMuted(action.Target, true),
                cancellationToken),
            AutomationActionType.UnmuteApplication => await RunAudioAsync(
                action,
                () => _audioService.SetApplicationMuted(action.Target, false),
                cancellationToken),
            AutomationActionType.StartFocus => StartTimer(action, FocusSessionKind.Focus),
            AutomationActionType.StartBreak => StartTimer(action, FocusSessionKind.Break),
            AutomationActionType.CreateTask => CreateTask(action),
            _ => AutomationActionResult.Failed(
                action,
                "Acción no disponible",
                "Kohana bloqueó una acción que no pertenece a la lista permitida.")
        };
    }

    private static AutomationActionResult OpenApplication(AutomationAction action)
    {
        var workingDirectory = ResolveDirectory(action.WorkingDirectory);
        var info = new ProcessStartInfo
        {
            FileName = action.Target,
            Arguments = action.Arguments,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            info.WorkingDirectory = workingDirectory;
        }

        Process.Start(info);
        return AutomationActionResult.Completed(
            action,
            "Aplicación abierta",
            $"Abrí {action.Target}.");
    }

    private static AutomationActionResult OpenFolder(AutomationAction action)
    {
        var directory = ResolveDirectory(action.WorkingDirectory);
        if (!Directory.Exists(directory))
        {
            return AutomationActionResult.Failed(
                action,
                "Carpeta no encontrada",
                $"No encontré {directory}.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{directory}\"",
            UseShellExecute = true
        });
        return AutomationActionResult.Completed(
            action,
            "Carpeta abierta",
            $"Abrí {directory}.");
    }

    private static AutomationActionResult OpenTerminal(AutomationAction action)
    {
        var directory = ResolveDirectory(action.WorkingDirectory);
        if (!Directory.Exists(directory))
        {
            return AutomationActionResult.Failed(
                action,
                "Carpeta no encontrada",
                $"No encontré {directory}.");
        }

        var escaped = directory.Replace("'", "''", StringComparison.Ordinal);
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoExit -Command \"Set-Location -LiteralPath '{escaped}'\"",
            WorkingDirectory = directory,
            UseShellExecute = true
        });
        return AutomationActionResult.Completed(
            action,
            "Terminal abierta",
            $"Abrí PowerShell en {directory}.");
    }

    private static async Task<AutomationActionResult> RunAudioAsync(
        AutomationAction action,
        Func<AudioActionResult> operation,
        CancellationToken cancellationToken)
    {
        var result = await Task.Run(operation, cancellationToken);
        return result.Succeeded
            ? AutomationActionResult.Completed(action, result.Title, result.Detail)
            : AutomationActionResult.Failed(action, result.Title, result.Detail);
    }

    private AutomationActionResult StartTimer(
        AutomationAction action,
        FocusSessionKind kind)
    {
        var minutes = (int)Math.Round(action.NumericValue ?? 0);
        var label = string.IsNullOrWhiteSpace(action.Text)
            ? kind == FocusSessionKind.Break ? "Descanso" : "Sesión de enfoque"
            : action.Text.Trim();
        var result = _focusManager.Start(
            TimeSpan.FromMinutes(minutes),
            label,
            kind,
            DateTimeOffset.Now);

        return result.Success
            ? AutomationActionResult.Completed(action, "Temporizador iniciado", result.Message)
            : AutomationActionResult.Failed(action, "No pude iniciar el temporizador", result.Message);
    }

    private AutomationActionResult CreateTask(AutomationAction action)
    {
        var task = _taskManager.Create(action.Text);
        return AutomationActionResult.Completed(
            action,
            "Tarea creada",
            $"Agregué {task.Title}.");
    }

    private static string ResolveDirectory(string value)
    {
        if (!value.Equals("{project}", StringComparison.OrdinalIgnoreCase))
        {
            return Environment.ExpandEnvironmentVariables(value.Trim());
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Nexo.slnx")) ||
                File.Exists(Path.Combine(current.FullName, "Kohana.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Environment.CurrentDirectory;
    }
}
