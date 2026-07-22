using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexo.Core.Metrics;
using Nexo.Core.Resources;

namespace Nexo.App.Views;

public partial class SystemView : UserControl
{
    public SystemView()
    {
        InitializeComponent();
    }

    public event EventHandler? RestartVoiceRequested;

    public event EventHandler? DiagnosticsRequested;

    public void UpdateSnapshot(SystemSnapshot snapshot)
    {
        CpuValueText.Text = FormatPercentage(snapshot.CpuUsagePercent);
        MemoryValueText.Text = FormatPercentage(snapshot.MemoryUsagePercent);
        GpuValueText.Text = FormatPercentage(snapshot.GpuUsagePercent);
        GpuMemoryText.Text = snapshot.DedicatedGpuMemoryBytes.HasValue
            ? $"VRAM usada: {FormatBytes(snapshot.DedicatedGpuMemoryBytes.Value)}"
            : "VRAM no disponible";
        DiskValueText.Text = FormatPercentage(snapshot.SystemDriveUsagePercent);

        TopProcessNameText.Text = string.IsNullOrWhiteSpace(snapshot.TopProcessName)
            ? "No disponible"
            : snapshot.TopProcessName;

        TopProcessMemoryText.Text = snapshot.TopProcessWorkingSetBytes.HasValue
            ? FormatBytes(snapshot.TopProcessWorkingSetBytes.Value)
            : string.Empty;

        var health = SystemHealthEvaluator.Evaluate(snapshot);
        HealthText.Text = SystemHealthEvaluator.GetLabel(health);
        HealthText.Foreground = health == SystemHealthState.Ready
            ? (Brush)FindResource("BrushSuccess")
            : (Brush)FindResource("BrushWarning");

        UpdatedAtText.Text = snapshot.CapturedAt == DateTimeOffset.MinValue
            ? string.Empty
            : snapshot.CapturedAt.ToString("HH:mm:ss");
    }

    public void UpdateRuntimeStatus(
        bool voiceReady,
        bool wakeWordEnabled,
        bool wakeWordListening,
        bool visionEnabled,
        string aiStatus,
        bool aiHealthy,
        ResourceMode resourceMode,
        string resourceReason)
    {
        RuntimeVoiceText.Text = wakeWordEnabled
            ? wakeWordListening ? "Atenta" : "Pausada"
            : voiceReady ? "Micrófono listo" : "No preparada";
        RuntimeVoiceText.Foreground = wakeWordListening || (!wakeWordEnabled && voiceReady)
            ? (Brush)FindResource("BrushSuccess")
            : (Brush)FindResource("BrushWarning");

        RuntimeAiText.Text = string.IsNullOrWhiteSpace(aiStatus) ? "Desactivada" : aiStatus;
        RuntimeAiText.Foreground = aiHealthy
            ? (Brush)FindResource("BrushSuccess")
            : (Brush)FindResource("BrushTextSecondary");

        RuntimeVisionText.Text = visionEnabled ? "Activa bajo demanda" : "Desactivada";
        RuntimeVisionText.Foreground = visionEnabled
            ? (Brush)FindResource("BrushSuccess")
            : (Brush)FindResource("BrushTextSecondary");

        RuntimeModeText.Text = resourceMode switch
        {
            ResourceMode.Game => "Modo Juego",
            ResourceMode.Busy => "Protegiendo recursos",
            _ => "Normal"
        };
        RuntimeModeText.Foreground = resourceMode == ResourceMode.Normal
            ? (Brush)FindResource("BrushSuccess")
            : (Brush)FindResource("BrushWarning");
        RuntimeDetailText.Text = string.IsNullOrWhiteSpace(resourceReason)
            ? "Kohana está lista."
            : resourceReason;
    }

    private void RestartVoiceButton_Click(object sender, RoutedEventArgs e) =>
        RestartVoiceRequested?.Invoke(this, EventArgs.Empty);

    private void DiagnosticsButton_Click(object sender, RoutedEventArgs e) =>
        DiagnosticsRequested?.Invoke(this, EventArgs.Empty);

    private static string FormatPercentage(double? value) =>
        value.HasValue ? $"{value.Value:0}%" : "—";

    private static string FormatBytes(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        const double gigabyte = 1024d * 1024d * 1024d;

        return bytes >= gigabyte
            ? $"{bytes / gigabyte:0.0} GB"
            : $"{bytes / megabyte:0} MB";
    }
}
