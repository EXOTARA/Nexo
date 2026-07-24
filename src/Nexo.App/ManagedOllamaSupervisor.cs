using System.IO;
using Nexo.Core.Ai;
using Nexo.Core.Diagnostics;
using Nexo.Core.Settings;
using Nexo.Windows.Ai;


namespace Nexo.App;

public sealed class ManagedOllamaSupervisor : IDisposable
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(15);

    private readonly OllamaRuntimeService _runtimeService = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _ensureGate = new(1, 1);

    private Action<OllamaRuntimeSnapshot>? _snapshotChanged;
    private Task? _monitorTask;
    private bool _enabled;
    private bool _stopRequested;
    private bool _disposed;

    public bool Configure(ShellPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        _enabled = preferences.AiProvider == AiProviderKind.Ollama &&
                   OllamaRuntimeEndpoints.IsManagedBaseUrl(preferences.AiBaseUrl);
        return _enabled;
    }

    public void StartMonitoring(Action<OllamaRuntimeSnapshot> snapshotChanged)
    {
        ArgumentNullException.ThrowIfNull(snapshotChanged);
        ThrowIfDisposed();

        _snapshotChanged = snapshotChanged;
        if (!_enabled || _monitorTask is { IsCompleted: false })
        {
            return;
        }

        _monitorTask = MonitorAsync(_lifetimeCancellation.Token);
    }

    public async Task<OllamaRuntimeSnapshot> EnsureRunningAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_enabled)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.Unavailable,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                null,
                "La IA administrada por Kohana no está configurada.");
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);

        await _ensureGate.WaitAsync(linkedCancellation.Token);
        try
        {
            return await _runtimeService.StartManagedAsync(linkedCancellation.Token);
        }
        finally
        {
            _ensureGate.Release();
        }
    }

    /// <summary>
    /// Detiene el monitor y el runtime administrado de forma **asíncrona** e idempotente.
    /// Debe llamarse desde la ruta de salida (mientras el Dispatcher aún bombea, antes de
    /// <c>Application.Shutdown</c>): así las continuaciones de <see cref="OllamaRuntimeService.StopManagedAsync"/>
    /// se procesan con normalidad. Antes, este trabajo se hacía con
    /// <c>GetAwaiter().GetResult()</c> dentro de <see cref="Dispose"/> durante
    /// <c>App.OnExit</c>, cuando el Dispatcher ya no bombea: la continuación quedaba
    /// encolada en un hilo bloqueado, el proceso no terminaba y quedaba una instancia
    /// fantasma que retenía la coordinación de instancia única.
    /// </summary>
    public async Task StopAsync()
    {
        if (_stopRequested)
        {
            return;
        }

        _stopRequested = true;
        _lifetimeCancellation.Cancel();

        try
        {
            await _runtimeService.StopManagedAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            WriteLog($"No se pudo detener el runtime administrado: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetimeCancellation.Cancel();

        // La parada del proceso administrado ocurre de forma asíncrona en StopAsync, que la
        // ruta de salida llama antes de Application.Shutdown. Aquí NO se hace
        // sync-sobre-async: bloquear el hilo de UI con GetResult() durante App.OnExit
        // (Dispatcher sin bombear) interbloqueaba y dejaba el proceso fantasma. Este Dispose
        // solo libera recursos propios y es idempotente.
        _runtimeService.Dispose();
        _ensureGate.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        OllamaRuntimeState? lastState = null;
        string? lastMessage = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_enabled)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            OllamaRuntimeSnapshot snapshot;
            try
            {
                snapshot = await EnsureRunningAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested || _disposed)
                {
                    break;
                }

                snapshot = new OllamaRuntimeSnapshot(
                    OllamaRuntimeState.ManagedInstalled,
                    OllamaRuntimeEndpoints.ManagedBaseUrl,
                    NexoDataPaths.OllamaExecutable,
                    $"No pude iniciar la IA local: {exception.Message}");
            }

            if (cancellationToken.IsCancellationRequested || _disposed)
            {
                break;
            }

            if (snapshot.State != lastState ||
                !string.Equals(snapshot.Message, lastMessage, StringComparison.Ordinal))
            {
                WriteLog($"{snapshot.State}: {snapshot.Message}");
                _snapshotChanged?.Invoke(snapshot);
                lastState = snapshot.State;
                lastMessage = snapshot.Message;
            }

            try
            {
                await Task.Delay(MonitorInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _monitorTask = null;
    }

    private static void WriteLog(string message)
    {
        try
        {
            Directory.CreateDirectory(NexoDataPaths.LogsDirectory);
            File.AppendAllText(
                NexoDataPaths.OllamaRuntimeLog,
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // El diagnóstico no debe impedir que Nexo abra o cierre.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
