using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Nexo.Core.Diagnostics;
using Nexo.Core.Settings;
using Nexo.Core.Voice;
using Nexo.Windows.Diagnostics;
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
        bool startupEnabled)
    {
        InitializeComponent();
        _preferences = preferences;
        _voiceDevices = voiceDevices;
        _whisperReady = whisperReady;
        _wakeWordReady = wakeWordReady;
        _wakeWordListening = wakeWordListening;
        _trayActive = trayActive;
        _startupEnabled = startupEnabled;
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
