using System.Windows.Controls;
using System.Windows.Media;
using Nexo.Core.Metrics;

namespace Nexo.App.Views;

public partial class SystemView : UserControl
{
    public SystemView()
    {
        InitializeComponent();
    }

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

    private static string FormatPercentage(double? value)
    {
        return value.HasValue ? $"{value.Value:0}%" : "—";
    }

    private static string FormatBytes(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        const double gigabyte = 1024d * 1024d * 1024d;

        return bytes >= gigabyte
            ? $"{bytes / gigabyte:0.0} GB"
            : $"{bytes / megabyte:0} MB";
    }
}
