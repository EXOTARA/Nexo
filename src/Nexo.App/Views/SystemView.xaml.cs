using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Hardware;
using Nexo.Core.Metrics;
using Nexo.Core.Resources;

namespace Nexo.App.Views;

public partial class SystemView : UserControl
{
    private readonly ObservableCollection<AdaptiveEnginePlanRow> _adaptiveEnginePlanRows = [];

    public SystemView()
    {
        InitializeComponent();
        AdaptiveEnginePlanItemsControl.ItemsSource = _adaptiveEnginePlanRows;
    }

    public event EventHandler? RestartVoiceRequested;

    public event EventHandler? DiagnosticsRequested;

    public event EventHandler? HardwareCapabilityRefreshRequested;

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

    public void ShowHardwareCapabilityUpdating()
    {
        HardwareCapabilityRefreshButton.IsEnabled = false;
        HardwareCapabilitySummaryText.Text = "Actualizando la detección de hardware…";
    }

    public void UpdateHardwareCapability(HardwareCapabilityProfile profile)
    {
        HardwareCapabilityRefreshButton.IsEnabled = true;

        HardwareCapabilityTierText.Text = DescribeTier(profile.Tier);
        HardwareCapabilitySummaryText.Text = profile.Summary;

        var processor = profile.Snapshot.Processor;
        HardwareCapabilityCpuText.Text = string.IsNullOrWhiteSpace(processor.Name)
            ? "No disponible"
            : processor.Name;

        HardwareCapabilityCoresText.Text = processor.PhysicalCores.HasValue || processor.LogicalProcessors.HasValue
            ? $"{FormatCount(processor.PhysicalCores)} físicos · {FormatCount(processor.LogicalProcessors)} lógicos"
            : "No disponible";

        HardwareCapabilityRamText.Text = profile.Snapshot.Memory.TotalPhysicalBytes.HasValue
            ? FormatBytes((long)profile.Snapshot.Memory.TotalPhysicalBytes.Value)
            : "No disponible";

        HardwareCapabilityArchitectureText.Text = FormatArchitecture(processor);

        var graphics = profile.Snapshot.PreferredGraphicsAdapter;
        HardwareCapabilityGpuText.Text = string.IsNullOrWhiteSpace(graphics.Name)
            ? "No disponible"
            : graphics.Name;

        HardwareCapabilityGraphicsMemoryText.Text = graphics.DedicatedMemoryBytes.HasValue
            ? FormatBytes(graphics.DedicatedMemoryBytes.Value)
            : "No disponible";

        HardwareCapabilityBatteryText.Text = profile.Snapshot.HasBattery switch
        {
            true => "Presente",
            false => "No detectada",
            null => "No disponible"
        };

        HardwareCapabilityCompletenessText.Text = profile.HasCompleteData
            ? "La información de capacidad es completa."
            : "La información de capacidad es parcial.";

        HardwareCapabilityReasonsText.Text = profile.PositiveReasons.Count > 0
            ? "Razones: " + string.Join(" · ", profile.PositiveReasons.Select(reason => reason.Message))
            : string.Empty;

        HardwareCapabilityMissingDataText.Text = profile.MissingData.Count > 0
            ? "Datos desconocidos: " + string.Join(" · ", profile.MissingData.Select(reason => reason.Message))
            : string.Empty;
    }

    public void UpdateAdaptiveEnginePlan(AdaptiveEnginePlan plan, IReadOnlyList<EngineDescriptor> descriptors)
    {
        AdaptiveEnginePlanModeText.Text = DescribeMode(plan.Mode);
        AdaptiveEnginePlanSummaryText.Text = plan.Summary;
        AdaptiveEnginePlanConfidenceText.Text = plan.GeneralWarnings.Count > 0
            ? string.Join(" ", plan.GeneralWarnings)
            : "La información de hardware es suficiente para esta recomendación.";

        _adaptiveEnginePlanRows.Clear();
        foreach (var recommendation in plan.Recommendations)
        {
            _adaptiveEnginePlanRows.Add(BuildAdaptiveEnginePlanRow(recommendation, descriptors));
        }
    }

    private static AdaptiveEnginePlanRow BuildAdaptiveEnginePlanRow(
        EngineRecommendation recommendation,
        IReadOnlyList<EngineDescriptor> descriptors)
    {
        var configuredLine = recommendation.ConfiguredEngineId is { } configuredId
            ? $"Configurado: {ResolveDisplayName(configuredId, descriptors)}"
            : "Configurado: ninguno";

        var activeLine = recommendation.ActiveEngineId is { } activeId
            ? $"Activo: {ResolveDisplayName(activeId, descriptors)}"
            : "Activo: desconocido";

        var recommendedLine = recommendation.RecommendedEngineId is { } recommendedId
            ? $"Recomendado: {ResolveDisplayName(recommendedId, descriptors)}"
            : "Recomendado: ninguna opción compatible con el hardware detectado";

        var reasonText = string.Join(" ", recommendation.Reasons);
        var warningText = string.Join(" ", recommendation.Warnings);

        return new AdaptiveEnginePlanRow(
            DescribeCategory(recommendation.Category),
            configuredLine,
            activeLine,
            recommendedLine,
            reasonText,
            warningText,
            WarningVisibility: warningText.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
            RecommendationOnlyVisibility: recommendation.IsRecommendationOnly ? Visibility.Visible : Visibility.Collapsed);
    }

    private static string ResolveDisplayName(EngineIdentifier id, IReadOnlyList<EngineDescriptor> descriptors) =>
        descriptors.FirstOrDefault(d => d.Id == id)?.DisplayName ?? id.Value;

    private static string DescribeMode(HardwarePerformanceMode mode) => mode switch
    {
        HardwarePerformanceMode.Automatic => "Automático",
        HardwarePerformanceMode.Eco => "Ahorro",
        HardwarePerformanceMode.Balanced => "Equilibrado",
        HardwarePerformanceMode.Maximum => "Máximo",
        _ => "Automático"
    };

    private static string DescribeCategory(EngineCategory category) => category switch
    {
        EngineCategory.SpeechToText => "Reconocimiento de voz",
        EngineCategory.WakeWord => "Palabra de activación",
        EngineCategory.TextToSpeech => "Síntesis de voz",
        EngineCategory.LocalLanguageModel => "Modelo de lenguaje",
        _ => category.ToString()
    };

    private sealed record AdaptiveEnginePlanRow(
        string CategoryLabel,
        string ConfiguredLine,
        string ActiveLine,
        string RecommendedLine,
        string ReasonText,
        string WarningText,
        Visibility WarningVisibility,
        Visibility RecommendationOnlyVisibility);

    private void RestartVoiceButton_Click(object sender, RoutedEventArgs e) =>
        RestartVoiceRequested?.Invoke(this, EventArgs.Empty);

    private void DiagnosticsButton_Click(object sender, RoutedEventArgs e) =>
        DiagnosticsRequested?.Invoke(this, EventArgs.Empty);

    private void HardwareCapabilityRefreshButton_Click(object sender, RoutedEventArgs e) =>
        HardwareCapabilityRefreshRequested?.Invoke(this, EventArgs.Empty);

    private static string DescribeTier(HardwareCapabilityTier tier) => tier switch
    {
        HardwareCapabilityTier.Basic => "Básico",
        HardwareCapabilityTier.Standard => "Estándar",
        HardwareCapabilityTier.Accelerated => "Acelerado",
        HardwareCapabilityTier.HighPerformance => "Alto rendimiento",
        _ => "Desconocido"
    };

    private static string FormatArchitecture(ProcessorCapability processor)
    {
        if (string.IsNullOrWhiteSpace(processor.OperatingSystemArchitecture))
        {
            return "No disponible";
        }

        if (!string.IsNullOrWhiteSpace(processor.ProcessArchitecture) &&
            processor.ProcessArchitecture != processor.OperatingSystemArchitecture)
        {
            return $"{processor.OperatingSystemArchitecture} (proceso: {processor.ProcessArchitecture})";
        }

        return processor.OperatingSystemArchitecture;
    }

    private static string FormatCount(int? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "?";

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
