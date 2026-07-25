namespace Nexo.Windows.Tests.Composition;

/// <summary>
/// Invariantes estructurales del hotfix de salida (Fase 1.3B3.1). Guardan la corrección del
/// proceso fantasma: el runtime de IA administrado se detiene de forma asíncrona antes de
/// <c>Application.Shutdown</c>, y ningún <c>Dispose</c> de la ruta de cierre bloquea el hilo
/// de UI con sync-sobre-async (lo que dejaba a <c>App.OnExit</c> sin alcanzar
/// <c>_singleInstance.Dispose()</c> y, por tanto, sin liberar la instancia única).
///
/// Se leen los archivos fuente porque <c>Nexo.App</c> no puede referenciarse desde pruebas
/// (arrastra <c>UseWPF</c>), igual que en <c>CompositionInvariantTests</c>.
/// </summary>
public sealed class ShutdownHotfixInvariantTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ManagedOllamaSupervisorDispose_HasNoSyncOverAsyncBlock()
    {
        // La causa raíz: Dispose hacía StopManagedAsync(...).GetAwaiter().GetResult() en el
        // hilo de UI durante App.OnExit, cuando el Dispatcher ya no bombea -> interbloqueo.
        var body = ExtractMethodBody(
            ReadManagedOllamaSupervisorSource(),
            "public void Dispose()",
            "private async Task MonitorAsync(");

        Assert.DoesNotContain("GetAwaiter().GetResult()", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".Result", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".Wait(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("StopManagedAsync", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ManagedOllamaSupervisor_StopsTheRuntimeAsynchronouslyAndIdempotently()
    {
        var content = ReadManagedOllamaSupervisorSource();
        var body = ExtractMethodBody(
            content,
            "public async Task StopAsync()",
            "public void Dispose()");

        // Detención asíncrona real del runtime, con guarda de una sola ejecución.
        Assert.Contains("await _runtimeService.StopManagedAsync(", body, StringComparison.Ordinal);
        Assert.Contains("_stopRequested", body, StringComparison.Ordinal);
        Assert.Contains("_lifetimeCancellation.Cancel();", body, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult()", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestExit_StartsShutdownOnce_WithoutBlockingOrCallingShutdownDirectly()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private void RequestExit()",
            "private async Task RequestExitAsync()");

        // Un solo inicio de apagado y ruta asíncrona; RequestExit no llama Shutdown directamente.
        Assert.Contains("_isClosed || _exitRequested", body, StringComparison.Ordinal);
        Assert.Contains("_exitRequested = true;", body, StringComparison.Ordinal);
        Assert.Contains("_ = RequestExitAsync();", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Application.Current.Shutdown()", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestExitAsync_StopsTheManagedRuntimeBeforeShutdown()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private async Task RequestExitAsync()",
            "private static string FormatPercentage(");

        var stopIndex = body.IndexOf(
            "await _managedOllamaSupervisor.StopAsync();", StringComparison.Ordinal);
        var shutdownIndex = body.IndexOf(
            "System.Windows.Application.Current.Shutdown();", StringComparison.Ordinal);

        Assert.True(stopIndex >= 0, "RequestExitAsync debe detener el runtime administrado.");
        Assert.True(shutdownIndex > stopIndex,
            "El apagado del runtime debe ocurrir antes de Application.Shutdown.");

        // Shutdown se ejecuta pase lo que pase (en el finally), aunque la parada falle.
        var finallyIndex = body.IndexOf("finally", StringComparison.Ordinal);
        Assert.True(finallyIndex >= 0 && finallyIndex < shutdownIndex,
            "Application.Shutdown debe llamarse desde el finally de RequestExitAsync.");
    }

    [Fact]
    public void AppOnExit_DisposesInNonBlockingOrder_ReachingSingleInstance()
    {
        var body = ExtractMethodBody(
            ReadAppSource(),
            "protected override void OnExit(ExitEventArgs e)",
            null);

        var ollamaIndex = body.IndexOf("_managedOllamaSupervisor?.Dispose();", StringComparison.Ordinal);
        var compositionIndex = body.IndexOf("_compositionRoot?.Dispose();", StringComparison.Ordinal);
        var singleInstanceIndex = body.IndexOf("_singleInstance?.Dispose();", StringComparison.Ordinal);

        Assert.True(ollamaIndex >= 0
            && compositionIndex > ollamaIndex
            && singleInstanceIndex > compositionIndex,
            "El orden de OnExit (ollama -> compositionRoot -> singleInstance) cambió.");

        // OnExit no debe bloquear el hilo de UI: la fase asíncrona vive en RequestExitAsync.
        Assert.DoesNotContain("GetAwaiter().GetResult()", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".Result", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".Wait(", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Shutdown_StillDisposesTheVoiceSubsystemInTheCompositionRoot_Phase1_3B3Preserved()
    {
        // El hotfix no revierte la propiedad de 1.3B3: el composition root sigue liberando
        // los tres servicios de voz, y MainWindow sigue sin liberarlos.
        var root = ReadCompositionRootSource();
        Assert.Contains("WakeWordService.Dispose();", root, StringComparison.Ordinal);
        Assert.Contains("VoiceOutputService.Dispose();", root, StringComparison.Ordinal);
        Assert.Contains("VoiceInputService.Dispose();", root, StringComparison.Ordinal);

        var mainWindow = ReadMainWindowSource();
        Assert.DoesNotContain("_voiceInputService", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_wakeWordService", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceOutputService", mainWindow, StringComparison.Ordinal);
    }

    private static string ReadManagedOllamaSupervisorSource() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "ManagedOllamaSupervisor.cs"));

    private static string ReadMainWindowSource() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "MainWindow.xaml.cs"));

    private static string ReadAppSource() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "App.xaml.cs"));

    private static string ReadCompositionRootSource() =>
        File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "Nexo.Windows", "Composition", "KohanaCompositionRoot.cs"));

    private static string ExtractMethodBody(string content, string startMarker, string? endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"No se encontró '{startMarker}' en el archivo.");

        if (endMarker is null)
        {
            return content[start..];
        }

        var end = content.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"No se encontró '{endMarker}' después de '{startMarker}'.");
        return content[start..end];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Nexo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró Nexo.slnx desde el directorio de pruebas.");
    }
}
