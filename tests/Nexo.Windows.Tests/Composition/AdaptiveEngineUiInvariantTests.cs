namespace Nexo.Windows.Tests.Composition;

/// <summary>
/// Invariantes estructurales de la Fase 2.2 (Adaptive Engine Registry y modos de rendimiento)
/// que no pueden expresarse como una llamada a la API: dependen del contenido de los archivos
/// de <c>Nexo.App</c>, que no se referencia desde pruebas porque arrastra <c>UseWPF</c> (ver
/// notas de la Fase 1.1 en <see cref="CompositionInvariantTests"/>).
/// </summary>
public sealed class AdaptiveEngineUiInvariantTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void SettingsView_ExposesTheFourPerformanceModesAsRadioButtons()
    {
        var content = ReadSettingsViewXaml();

        Assert.Contains("Tag=\"Automatic\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Eco\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Balanced\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Maximum\"", content, StringComparison.Ordinal);
        Assert.Equal(4, CountOccurrences(content, "GroupName=\"HardwarePerformanceMode\""));
    }

    [Fact]
    public void SettingsView_PerformanceModeOptions_HaveAccessibleNames()
    {
        var content = ReadSettingsViewXaml();

        Assert.Contains("AutomationProperties.Name=\"Modo de rendimiento: Automático\"", content, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Modo de rendimiento: Ahorro\"", content, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Modo de rendimiento: Equilibrado\"", content, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Modo de rendimiento: Máximo\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsView_DoesNotReuseTheResourceGovernorSectionTitle()
    {
        // La sección existente "Rendimiento adaptativo" pertenece al Resource Governor (modo
        // Normal/Ocupado/Juego, reactivo). La nueva sección de modos usa un título distinto
        // ("Modo de rendimiento") para no confundir ambos conceptos.
        var content = ReadSettingsViewXaml();

        Assert.Equal(1, CountOccurrences(content, "Text=\"Rendimiento adaptativo\""));
        Assert.Contains("Text=\"Modo de rendimiento\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsView_SavesImmediatelyOnPerformanceModeChange()
    {
        var content = ReadSettingsViewCodeBehind();

        Assert.Contains("HardwarePerformanceModeChanged?.Invoke(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_PersistsPerformanceModeAndRecalculatesOnChange()
    {
        var content = ReadMainWindowSource();

        var handlerStart = content.IndexOf("_settingsView.HardwarePerformanceModeChanged += mode =>", StringComparison.Ordinal);
        Assert.True(handlerStart >= 0, "No se encontró el manejador de HardwarePerformanceModeChanged.");

        var handlerEnd = content.IndexOf("};", handlerStart, StringComparison.Ordinal);
        var handlerBody = content[handlerStart..handlerEnd];

        Assert.Contains("_preferences.HardwarePerformanceMode = mode;", handlerBody, StringComparison.Ordinal);
        Assert.Contains("SavePreferences();", handlerBody, StringComparison.Ordinal);
        Assert.Contains("RefreshAdaptiveEnginePlan();", handlerBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_NeverAppliesModeChangesToEngines()
    {
        // Condición crítica del sprint: cambiar de modo nunca debe reemplazar motores,
        // descargar modelos, ni reiniciar servicios.
        var handlerStart = ReadMainWindowSource()
            .IndexOf("_settingsView.HardwarePerformanceModeChanged += mode =>", StringComparison.Ordinal);
        Assert.True(handlerStart >= 0);

        var content = ReadMainWindowSource();
        var handlerEnd = content.IndexOf("};", handlerStart, StringComparison.Ordinal);
        var handlerBody = content[handlerStart..handlerEnd];

        Assert.DoesNotContain("WhisperVoiceInputService", handlerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("VoskWakeWordService", handlerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsTextToSpeechService", handlerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureRunningAsync", handlerBody, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemView_HasAnAdaptivePlanSectionBelowHardwareCapability()
    {
        var content = ReadSystemViewXaml();

        var hardwareIndex = content.IndexOf("Capacidad del equipo", StringComparison.Ordinal);
        var planIndex = content.IndexOf("Plan adaptativo", StringComparison.Ordinal);

        Assert.True(hardwareIndex >= 0, "No se encontró la sección de Capacidad del equipo.");
        Assert.True(planIndex > hardwareIndex, "Plan adaptativo debe aparecer después de Capacidad del equipo.");
    }

    [Fact]
    public void SystemView_UsesTheRecommendationOnlyLabelWhenApplicable()
    {
        var content = ReadSystemViewXaml();

        Assert.Contains("Text=\"Solo recomendación\"", content, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding RecommendationOnlyVisibility}\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemView_AdaptivePlanSection_UsesOnlyDynamicResourceForColors()
    {
        var content = ReadSystemViewXaml();
        var planStart = content.IndexOf("Plan adaptativo", StringComparison.Ordinal);
        var planEnd = content.IndexOf("Mayor uso de memoria", StringComparison.Ordinal);
        Assert.True(planStart >= 0 && planEnd > planStart);

        var planSection = content[planStart..planEnd];

        Assert.DoesNotContain("Background=\"#", planSection, StringComparison.Ordinal);
        Assert.DoesNotContain("Foreground=\"#", planSection, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsWindow_AddsAdaptiveEngineRowsDuringRefresh()
    {
        var content = ReadDiagnosticsWindowCodeBehind();

        var refreshStart = content.IndexOf("private async Task RefreshAsync()", StringComparison.Ordinal);
        var refreshEnd = content.IndexOf("catch (Exception exception)", refreshStart, StringComparison.Ordinal);
        Assert.True(refreshStart >= 0 && refreshEnd > refreshStart);

        var refreshBody = content[refreshStart..refreshEnd];
        Assert.Contains("AddAdaptiveEngineRows();", refreshBody, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsWindow_NeverExposesApiKeysOrSecrets()
    {
        var content = ReadDiagnosticsWindowCodeBehind();
        var rowsStart = content.IndexOf("private void AddAdaptiveEngineRows()", StringComparison.Ordinal);
        var rowsEnd = content.IndexOf("private static string DescribeTriState", rowsStart, StringComparison.Ordinal);
        Assert.True(rowsStart >= 0 && rowsEnd > rowsStart);

        var rowsBody = content[rowsStart..rowsEnd];
        Assert.DoesNotContain("ApiKey", rowsBody, StringComparison.Ordinal);
        Assert.DoesNotContain("AiApiKeyEnvironmentVariable", rowsBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadApiKey", rowsBody, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string content, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = content.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }

    private static string ReadSettingsViewXaml() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "Views", "SettingsView.xaml"));

    private static string ReadSettingsViewCodeBehind() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "Views", "SettingsView.xaml.cs"));

    private static string ReadSystemViewXaml() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "Views", "SystemView.xaml"));

    private static string ReadDiagnosticsWindowCodeBehind() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "DiagnosticsWindow.xaml.cs"));

    private static string ReadMainWindowSource() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "MainWindow.xaml.cs"));

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
