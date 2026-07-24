using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Nexo.Core.Diagnostics;
using Nexo.Core.Hardware;
using Nexo.Core.Settings;
using Nexo.Core.Voice;
using Nexo.Windows.Diagnostics;
using Nexo.Windows.Updates;
using System.IO;

namespace Nexo.App;

public partial class DiagnosticsWindow : Window
{
    private readonly ShellPreferences _preferences;
    private readonly IReadOnlyList<VoiceInputDevice> _voiceDevices;
    private readonly bool _whisperReady;
    private readonly bool _wakeWordReady;
    private readonly bool _wakeWordListening;
    private readonly bool _trayActive;
    private readonly bool _startupEnabled;
    private readonly IHardwareCapabilityService _hardwareCapabilityService;
    private readonly NexoDiagnosticService _diagnosticService = new();
    private readonly ObservableCollection<DiagnosticItemRow> _items = [];
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private NexoDiagnosticSnapshot? _snapshot;

    public DiagnosticsWindow(
        ShellPreferences preferences,
        IReadOnlyList<VoiceInputDevice> voiceDevices,
        bool whisperReady,
        bool wakeWordReady,
        bool wakeWordListening,
        bool trayActive,
        bool startupEnabled,
        IHardwareCapabilityService hardwareCapabilityService)
    {
        InitializeComponent();
        _preferences = preferences;
        _voiceDevices = voiceDevices;
        _whisperReady = whisperReady;
        _wakeWordReady = wakeWordReady;
        _wakeWordListening = wakeWordListening;
        _trayActive = trayActive;
        _startupEnabled = startupEnabled;
        _hardwareCapabilityService = hardwareCapabilityService
            ?? throw new ArgumentNullException(nameof(hardwareCapabilityService));
        ItemsControl.ItemsSource = _items;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async Task RefreshAsync()
    {
        SummaryText.Text = "Comprobando servicios…";
        try
        {
            _snapshot = await _diagnosticService.CaptureAsync(
                _preferences,
                _voiceDevices,
                _whisperReady,
                _wakeWordReady,
                _wakeWordListening,
                _trayActive,
                _startupEnabled,
                _lifetimeCancellation.Token);
            _items.Clear();
            foreach (var item in _snapshot.Items)
            {
                _items.Add(new DiagnosticItemRow(
                    item.Name,
                    item.Detail,
                    ResolveStatusBrush(item.Status)));
            }

            AddHardwareCapabilityRows();

            var warnings = _snapshot.Items.Count(item =>
                item.Status is DiagnosticStatus.Warning or DiagnosticStatus.Unavailable);
            SummaryText.Text = warnings == 0
                ? "Los servicios revisados están disponibles."
                : $"Encontré {warnings} elemento(s) que requieren atención.";
        }
        catch (Exception exception)
        {
            SummaryText.Text = $"No pude completar el diagnóstico: {exception.Message}";
        }
    }

    private void AddHardwareCapabilityRows()
    {
        var profile = _hardwareCapabilityService.GetCachedProfile();
        var snapshot = profile.Snapshot;

        _items.Add(new DiagnosticItemRow(
            "Capacidad de hardware",
            $"Nivel: {profile.Tier} · Confianza: {profile.OverallConfidence} · " +
            $"Detectado: {(snapshot.CapturedAt == DateTimeOffset.MinValue ? "aún no" : snapshot.CapturedAt.ToString("g"))} · " +
            $"Origen: {(snapshot.WasCached ? "caché" : "detección reciente")}",
            ResolveStatusBrush(profile.HasCompleteData ? DiagnosticStatus.Ready : DiagnosticStatus.Information)));

        _items.Add(new DiagnosticItemRow(
            "CPU detectado",
            $"Nombre: {snapshot.Processor.Name ?? "desconocido"} · Fabricante: {snapshot.Processor.Vendor ?? "desconocido"} · " +
            $"Núcleos físicos: {FormatNumber(snapshot.Processor.PhysicalCores)} · " +
            $"Procesadores lógicos: {FormatNumber(snapshot.Processor.LogicalProcessors)}",
            ResolveStatusBrush(DiagnosticStatus.Information)));

        _items.Add(new DiagnosticItemRow(
            "RAM detectada",
            snapshot.Memory.TotalPhysicalBytes.HasValue
                ? $"{snapshot.Memory.TotalPhysicalBytes.Value / (1024d * 1024 * 1024):0.0} GB"
                : "Desconocida",
            ResolveStatusBrush(DiagnosticStatus.Information)));

        if (snapshot.GraphicsAdapters.Count == 0)
        {
            _items.Add(new DiagnosticItemRow(
                "GPU",
                "No se detectó ninguna GPU.",
                ResolveStatusBrush(DiagnosticStatus.Warning)));
        }
        else
        {
            foreach (var adapter in snapshot.GraphicsAdapters)
            {
                var dedicated = adapter.IsDedicated switch
                {
                    true => "sí",
                    false => "no",
                    null => "desconocido"
                };
                var vram = adapter.DedicatedMemoryBytes.HasValue
                    ? $"{adapter.DedicatedMemoryBytes.Value / (1024d * 1024 * 1024):0.0} GB"
                    : "desconocida";

                _items.Add(new DiagnosticItemRow(
                    $"GPU: {adapter.Name ?? "desconocida"}",
                    $"Dedicada: {dedicated} · Memoria dedicada: {vram}",
                    ResolveStatusBrush(DiagnosticStatus.Information)));
            }
        }

        if (profile.MissingData.Count > 0)
        {
            _items.Add(new DiagnosticItemRow(
                "Datos de hardware incompletos",
                string.Join(" · ", profile.MissingData.Select(reason => reason.Message)),
                ResolveStatusBrush(DiagnosticStatus.Warning)));
        }
    }

    private static string FormatNumber(int? value) => value?.ToString() ?? "desconocido";

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
        {
            return;
        }

        Clipboard.SetText(_snapshot.ToClipboardText());
        SummaryText.Text = "Diagnóstico copiado. No incluye conversaciones ni claves.";
    }

    private void OpenDataButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(NexoDataPaths.RootDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = NexoDataPaths.RootDirectory,
            UseShellExecute = true
        });
    }


    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        SummaryText.Text = "Buscando una nueva versión…";

        try
        {
            using var service = new GitHubUpdateService(ReleaseMetadata.RepositoryUrl);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));

            var result = await service.CheckAsync(
                ReleaseMetadata.CurrentVersion,
                timeout.Token);

            SummaryText.Text = result.Message;
            if (!result.IsUpdateAvailable || string.IsNullOrWhiteSpace(result.ReleaseUrl))
            {
                return;
            }

            var decision = MessageBox.Show(
                $"{result.ReleaseName}\n\n" +
                $"Actual: {result.CurrentVersion}\n" +
                $"Disponible: {result.LatestVersion}\n\n" +
                "¿Quieres abrir la página de descarga?",
                "Actualización de Kohana",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (decision == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = result.ReleaseUrl,
                    UseShellExecute = true
                });
            }
        }
        catch (OperationCanceledException)
        {
            SummaryText.Text = "La comprobación de actualizaciones fue cancelada.";
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    private void ClearTemporaryButton_Click(object sender, RoutedEventArgs e)
    {
        SummaryText.Text = _diagnosticService.ClearTemporaryData();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private Brush ResolveStatusBrush(DiagnosticStatus status) => status switch
    {
        DiagnosticStatus.Ready => (Brush)FindResource("BrushSuccess"),
        DiagnosticStatus.Warning => (Brush)FindResource("BrushWarning"),
        DiagnosticStatus.Unavailable => (Brush)FindResource("BrushDanger"),
        _ => (Brush)FindResource("BrushTextSecondary")
    };

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _diagnosticService.Dispose();
        _lifetimeCancellation.Dispose();
    }

    public sealed record DiagnosticItemRow(
        string Name,
        string Detail,
        Brush StatusBrush);
}
