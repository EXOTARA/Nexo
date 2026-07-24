using Nexo.Core.Resources;
using Nexo.Core.Metrics;
using Nexo.Core.Settings;
using Nexo.Core.Vision;
using Nexo.Core.WindowsIntegration;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Fase 1.1 — congela arranque, cierre y las reglas del Resource Governor y de privacidad de
/// Vision tal como las compone hoy <c>MainWindow</c>.
///
/// Cubre los escenarios 12 (segunda instancia, parte de política), 13 (captura sensible) y
/// 12 del modo juego de `TEST_MATRIX.md`.
/// </summary>
public sealed class ShellLifecycleCharacterizationTests
{
    private static SystemSnapshot Idle() => new(
        CpuUsagePercent: 5,
        MemoryUsagePercent: 30,
        UsedMemoryBytes: 4_000_000_000,
        TotalMemoryBytes: 16_000_000_000,
        GpuUsagePercent: 4,
        DedicatedGpuMemoryBytes: null,
        SystemDriveUsagePercent: 40,
        TopProcessName: "explorer",
        TopProcessWorkingSetBytes: 100_000_000,
        CapturedAt: DateTimeOffset.Now);

    // ---------- Arranque ----------

    [Fact]
    public void Startup_HiddenModeIsOptIn()
    {
        Assert.False(StartupCommandBuilder.ShouldStartHidden([]));
        Assert.True(StartupCommandBuilder.ShouldStartHidden(["--background"]));
    }

    [Fact]
    public void Startup_UnknownArgumentsDoNotHideTheWindow()
    {
        Assert.False(StartupCommandBuilder.ShouldStartHidden(["--verbose", "-x"]));
    }

    [Fact]
    public void Startup_OnboardingIsPendingOnAFreshProfile()
    {
        // App.OnStartup muestra el onboarding cuando HasCompletedOnboarding es false,
        // y en ese caso ignora el arranque oculto.
        var preferences = new ShellPreferences();
        preferences.Normalize();

        Assert.False(preferences.HasCompletedOnboarding);
    }

    // ---------- Cierre ----------

    [Fact]
    public void Closing_HidesToTrayByDefault()
    {
        var preferences = new ShellPreferences();
        preferences.Normalize();

        Assert.True(preferences.MinimizeToTray);
        Assert.True(WindowsClosePolicy.ShouldHideInsteadOfClose(
            preferences.MinimizeToTray,
            explicitExitRequested: false));
    }

    [Fact]
    public void Closing_AnExplicitExitAlwaysWins()
    {
        Assert.False(WindowsClosePolicy.ShouldHideInsteadOfClose(
            minimizeToTray: true,
            explicitExitRequested: true));
    }

    [Fact]
    public void Closing_WithoutTrayModeTheWindowReallyCloses()
    {
        Assert.False(WindowsClosePolicy.ShouldHideInsteadOfClose(
            minimizeToTray: false,
            explicitExitRequested: false));
    }

    // ---------- Resource Governor ----------

    [Fact]
    public void GameMode_PausesWakeWordAndVisionButNeverLocalCommands()
    {
        var decision = ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
            Idle(),
            IsForegroundFullScreen: true,
            ForegroundProcessName: "eldenring",
            ForegroundWindowTitle: "Elden Ring",
            IsOnBattery: false));

        Assert.Equal(ResourceMode.Game, decision.Mode);
        Assert.True(decision.AllowLocalCommands);
        Assert.True(decision.PauseWakeWord);
        Assert.False(decision.AllowVision);
        Assert.False(decision.AllowLocalAi);
        Assert.False(decision.AllowRemoteAi);
        Assert.True(decision.SuppressTransientOverlays);
    }

    [Fact]
    public void GameMode_IgnoresKohanaAndShellSurfacesInFullScreen()
    {
        // Sin esto, la propia ventana de Kohana a pantalla completa activaría el modo juego.
        foreach (var process in new[] { "kohana", "nexo", "explorer", "snippingtool" })
        {
            var decision = ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
                Idle(),
                IsForegroundFullScreen: true,
                ForegroundProcessName: process,
                ForegroundWindowTitle: process,
                IsOnBattery: false));

            Assert.NotEqual(ResourceMode.Game, decision.Mode);
        }
    }

    [Fact]
    public void BusyMode_KeepsLocalCommandsAndRemoteAiAlive()
    {
        var busy = Idle() with { GpuUsagePercent = 95 };

        var decision = ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
            busy,
            IsForegroundFullScreen: false,
            ForegroundProcessName: "chrome",
            ForegroundWindowTitle: "Chrome",
            IsOnBattery: false));

        Assert.Equal(ResourceMode.Busy, decision.Mode);
        Assert.True(decision.AllowLocalCommands);
        Assert.True(decision.AllowRemoteAi);
        Assert.False(decision.AllowLocalAi);
        Assert.False(decision.AllowVision);
        Assert.False(decision.PauseWakeWord);
    }

    [Theory]
    [InlineData(88.0, null, 30.0)]
    [InlineData(null, 92.0, 30.0)]
    [InlineData(null, null, 92.0)]
    public void BusyThresholds_AreInclusive(double? gpu, double? cpu, double memory)
    {
        var snapshot = Idle() with
        {
            GpuUsagePercent = gpu,
            CpuUsagePercent = cpu,
            MemoryUsagePercent = memory
        };

        var decision = ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
            snapshot,
            IsForegroundFullScreen: false,
            ForegroundProcessName: "chrome",
            ForegroundWindowTitle: "Chrome",
            IsOnBattery: false));

        Assert.Equal(ResourceMode.Busy, decision.Mode);
    }

    [Fact]
    public void IdleMachine_RunsInNormalMode()
    {
        var decision = ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
            Idle(),
            IsForegroundFullScreen: false,
            ForegroundProcessName: "notepad",
            ForegroundWindowTitle: "Bloc de notas",
            IsOnBattery: false));

        Assert.Equal(ResourceMode.Normal, decision.Mode);
        Assert.True(decision.AllowLocalCommands);
        Assert.True(decision.AllowVision);
    }

    [Fact]
    public void GovernorThresholds_AreTheDocumentedOnes()
    {
        // `SAKURA`/`RESOURCE_GOVERNOR_V1` y el contrato de diseño §10 declaran 88/92/92.
        Assert.Equal(88, ResourceGovernorPolicy.BusyGpuThreshold);
        Assert.Equal(92, ResourceGovernorPolicy.BusyCpuThreshold);
        Assert.Equal(92, ResourceGovernorPolicy.BusyMemoryThreshold);
    }

    // ---------- Privacidad de Vision ----------

    [Theory]
    [InlineData("1password", null)]
    [InlineData("bitwarden", null)]
    [InlineData("keepassxc", null)]
    [InlineData("logonui", null)]
    [InlineData(null, "Seguridad de Windows")]
    [InlineData(null, "Administrador de credenciales")]
    [InlineData(null, "Mi contraseña del banco")]
    public void SensitiveWindows_AreRefusedBeforeAnythingElse(string? process, string? title)
    {
        Assert.True(VisionPrivacyPolicy.IsSensitive(title, process));
    }

    [Fact]
    public void OrdinaryWindows_AreNotTreatedAsSensitive()
    {
        Assert.False(VisionPrivacyPolicy.IsSensitive("Bloc de notas", "notepad"));
    }

    [Fact]
    public void SensitiveProcessMatching_AlsoCoversTheExecutableName()
    {
        Assert.True(VisionPrivacyPolicy.IsSensitive(null, "1password.exe"));
    }

    [Fact]
    public void VisionIsEnabledByDefault_ButGovernedWhenBusy()
    {
        var preferences = new ShellPreferences();
        preferences.Normalize();

        Assert.True(preferences.VisionEnabled);
        Assert.True(preferences.ProtectVisionWhenBusy);
    }
}
