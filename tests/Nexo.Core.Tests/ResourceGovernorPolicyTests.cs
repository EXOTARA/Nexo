using Nexo.Core.Metrics;
using Nexo.Core.Resources;

namespace Nexo.Core.Tests;

public sealed class ResourceGovernorPolicyTests
{
    [Fact]
    public void NormalMode_AllowsAiVisionAndWakeWord()
    {
        var decision = ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
            CreateSnapshot(cpu: 22, memory: 41, gpu: 18),
            IsForegroundFullScreen: false,
            ForegroundProcessName: "code",
            ForegroundWindowTitle: "MainWindow.xaml",
            IsOnBattery: false));

        Assert.Equal(ResourceMode.Normal, decision.Mode);
        Assert.True(decision.AllowLocalCommands);
        Assert.True(decision.AllowLocalAi);
        Assert.True(decision.AllowRemoteAi);
        Assert.True(decision.AllowVision);
        Assert.False(decision.PauseWakeWord);
    }

    [Fact]
    public void FullScreenApplication_EntersGameMode()
    {
        var decision = ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
            CreateSnapshot(cpu: 40, memory: 55, gpu: 70),
            IsForegroundFullScreen: true,
            ForegroundProcessName: "game",
            ForegroundWindowTitle: "Game",
            IsOnBattery: false));

        Assert.Equal(ResourceMode.Game, decision.Mode);
        Assert.True(decision.AllowLocalCommands);
        Assert.False(decision.AllowLocalAi);
        Assert.False(decision.AllowRemoteAi);
        Assert.False(decision.AllowVision);
        Assert.True(decision.PauseWakeWord);
        Assert.True(decision.SuppressTransientOverlays);
    }

    [Theory]
    [InlineData(93, 30, 20)]
    [InlineData(20, 93, 20)]
    [InlineData(20, 30, 89)]
    public void HighResourceUsage_EntersBusyMode(
        double cpu,
        double memory,
        double gpu)
    {
        var decision = ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
            CreateSnapshot(cpu, memory, gpu),
            IsForegroundFullScreen: false,
            ForegroundProcessName: "code",
            ForegroundWindowTitle: "Nexo",
            IsOnBattery: false));

        Assert.Equal(ResourceMode.Busy, decision.Mode);
        Assert.True(decision.AllowLocalCommands);
        Assert.False(decision.AllowLocalAi);
        Assert.True(decision.AllowRemoteAi);
        Assert.False(decision.AllowVision);
        Assert.False(decision.PauseWakeWord);
    }

    [Fact]
    public void NexoFullScreen_IsNotTreatedAsGame()
    {
        var decision = ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
            CreateSnapshot(cpu: 10, memory: 30, gpu: 5),
            IsForegroundFullScreen: true,
            ForegroundProcessName: "Nexo",
            ForegroundWindowTitle: "Nexo",
            IsOnBattery: false));

        Assert.Equal(ResourceMode.Normal, decision.Mode);
    }

    [Theory]
    [InlineData("SnippingTool")]
    [InlineData("ScreenClippingHost")]
    public void WindowsCaptureOverlay_IsNotTreatedAsGame(string processName)
    {
        var decision = ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
            CreateSnapshot(cpu: 10, memory: 30, gpu: 5),
            IsForegroundFullScreen: true,
            ForegroundProcessName: processName,
            ForegroundWindowTitle: "Captura de pantalla",
            IsOnBattery: false));

        Assert.Equal(ResourceMode.Normal, decision.Mode);
        Assert.False(decision.PauseWakeWord);
        Assert.True(decision.AllowVision);
    }

    private static SystemSnapshot CreateSnapshot(
        double cpu,
        double memory,
        double gpu) => new(
        CpuUsagePercent: cpu,
        MemoryUsagePercent: memory,
        UsedMemoryBytes: 8,
        TotalMemoryBytes: 16,
        GpuUsagePercent: gpu,
        DedicatedGpuMemoryBytes: null,
        SystemDriveUsagePercent: 50,
        TopProcessName: "code",
        TopProcessWorkingSetBytes: 100,
        CapturedAt: DateTimeOffset.Now);
}
