using System.ComponentModel;
using System.Diagnostics;
using Nexo.Core.Ai;
using Nexo.Core.Diagnostics;

namespace Nexo.Windows.Ai;

public sealed class OllamaRuntimeService :
    IOllamaRuntimeService,
    IDisposable
{
    private const string AiBaseUrl = "http://localhost:11434/v1";
    private const string TagsEndpoint = "http://localhost:11434/api/tags";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string _managedExecutablePath;

    public OllamaRuntimeService(
        HttpClient? httpClient = null,
        string? managedExecutablePath = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        _ownsClient = httpClient is null;
        _managedExecutablePath = string.IsNullOrWhiteSpace(managedExecutablePath)
            ? NexoDataPaths.OllamaExecutable
            : managedExecutablePath;
    }

    public async Task<OllamaRuntimeSnapshot> InspectAsync(
        CancellationToken cancellationToken = default)
    {
        var managedInstalled = File.Exists(_managedExecutablePath);
        var endpointRunning = await IsEndpointRunningAsync(cancellationToken);

        if (endpointRunning)
        {
            var managedRunning = managedInstalled &&
                IsManagedProcessRunning(_managedExecutablePath);

            if (managedRunning)
            {
                return new OllamaRuntimeSnapshot(
                    OllamaRuntimeState.ManagedRunning,
                    AiBaseUrl,
                    _managedExecutablePath,
                    "La IA local administrada por Nexo está funcionando.");
            }

            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ExternalRunning,
                AiBaseUrl,
                null,
                "Se detectó una instalación externa de Ollama en funcionamiento.");
        }

        if (managedInstalled)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                AiBaseUrl,
                _managedExecutablePath,
                "La IA local de Nexo está instalada, pero no está iniciada.");
        }

        return new OllamaRuntimeSnapshot(
            OllamaRuntimeState.Unavailable,
            AiBaseUrl,
            null,
            "Ollama no está disponible. Nexo puede instalarlo por ti.");
    }

    public async Task<OllamaRuntimeSnapshot> StartManagedAsync(
        CancellationToken cancellationToken = default)
    {
        var currentState = await InspectAsync(cancellationToken);

        if (currentState.IsRunning)
        {
            return currentState;
        }

        if (!File.Exists(_managedExecutablePath))
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.Unavailable,
                AiBaseUrl,
                null,
                "La IA local todavía no está instalada.");
        }

        var runtimeDirectory = Path.GetDirectoryName(_managedExecutablePath);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                AiBaseUrl,
                _managedExecutablePath,
                "La ruta de la IA local no es válida.");
        }

        Directory.CreateDirectory(runtimeDirectory);
        Directory.CreateDirectory(NexoDataPaths.OllamaModelsDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = _managedExecutablePath,
            Arguments = "serve",
            WorkingDirectory = runtimeDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        startInfo.Environment["OLLAMA_HOST"] = "127.0.0.1:11434";
        startInfo.Environment["OLLAMA_MODELS"] = NexoDataPaths.OllamaModelsDirectory;

        Process? process = null;

        try
        {
            process = Process.Start(startInfo);

            if (process is null)
            {
                return new OllamaRuntimeSnapshot(
                    OllamaRuntimeState.ManagedInstalled,
                    AiBaseUrl,
                    _managedExecutablePath,
                    "Windows no pudo iniciar la IA local.");
            }

            var started = await WaitForEndpointAsync(process, cancellationToken);

            if (!started)
            {
                return new OllamaRuntimeSnapshot(
                    OllamaRuntimeState.ManagedInstalled,
                    AiBaseUrl,
                    _managedExecutablePath,
                    process.HasExited
                        ? "Ollama se cerró antes de completar el inicio."
                        : "Ollama tardó demasiado en responder.");
            }

            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedRunning,
                AiBaseUrl,
                _managedExecutablePath,
                "La IA local administrada por Nexo está funcionando.");
        }
        catch (OperationCanceledException)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                AiBaseUrl,
                _managedExecutablePath,
                "El inicio de la IA local fue cancelado.");
        }
        catch (Win32Exception exception)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                AiBaseUrl,
                _managedExecutablePath,
                $"Windows no pudo iniciar Ollama: {exception.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                AiBaseUrl,
                _managedExecutablePath,
                "Windows impidió iniciar la IA local.");
        }
        catch (Exception exception)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                AiBaseUrl,
                _managedExecutablePath,
                $"No pude iniciar la IA local: {exception.Message}");
        }
        finally
        {
            process?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<bool> IsEndpointRunningAsync(
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            using var response = await _httpClient.GetAsync(
                TagsEndpoint,
                timeout.Token);

            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private async Task<bool> WaitForEndpointAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                return false;
            }

            if (await IsEndpointRunningAsync(cancellationToken))
            {
                return true;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(350),
                cancellationToken);
        }

        return false;
    }

    private static bool IsManagedProcessRunning(
        string expectedExecutablePath)
    {
        string normalizedExpectedPath;

        try
        {
            normalizedExpectedPath = Path.GetFullPath(expectedExecutablePath);
        }
        catch
        {
            return false;
        }

        foreach (var process in Process.GetProcessesByName("ollama"))
        {
            using (process)
            {
                try
                {
                    var processPath = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(processPath))
                    {
                        continue;
                    }

                    var normalizedProcessPath = Path.GetFullPath(processPath);
                    if (string.Equals(
                        normalizedProcessPath,
                        normalizedExpectedPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Win32Exception)
                {
                    // Windows puede impedir leer procesos de otra sesión.
                }
                catch (InvalidOperationException)
                {
                    // El proceso pudo cerrarse mientras se inspeccionaba.
                }
                catch (NotSupportedException)
                {
                    // La ruta del proceso no está disponible.
                }
            }
        }

        return false;
    }
}
