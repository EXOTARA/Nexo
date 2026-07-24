using Nexo.Core.Hardware;
using Nexo.Windows.Hardware;

namespace Nexo.Windows.Tests.Hardware;

public sealed class WindowsHardwareCapabilityServiceTests
{
    [Fact]
    public async Task RefreshAsync_WithFullDetection_PopulatesEveryField()
    {
        var (service, processor, memory, graphics, battery, windowsVersion) = CreateService();

        var profile = await service.RefreshAsync();

        Assert.False(profile.Snapshot.WasCached);
        Assert.Equal(HardwareDataConfidence.Known, profile.OverallConfidence);
        Assert.Empty(profile.MissingData);
        _ = processor;
        _ = memory;
        _ = graphics;
        _ = battery;
        _ = windowsVersion;
    }

    [Fact]
    public async Task RefreshAsync_WhenCpuSourceThrows_DegradesSafelyWithoutCrashing()
    {
        var (service, processor, _, _, _, _) = CreateService();
        processor.NameFactory = () => throw new InvalidOperationException("simulated CPU read failure");

        var profile = await service.RefreshAsync();

        Assert.NotEqual(default, profile.Snapshot.CapturedAt);
        Assert.Equal(HardwareDataConfidence.Known, profile.OverallConfidence);
    }

    [Fact]
    public async Task RefreshAsync_WhenGpuSourceThrows_LeavesRemainingDataIntact()
    {
        var (service, _, memory, graphics, _, _) = CreateService();
        memory.TotalPhysicalBytesFactory = () => 32UL * 1024 * 1024 * 1024;
        graphics.AdaptersFactory = () => throw new InvalidOperationException("simulated GPU enumeration failure");

        var profile = await service.RefreshAsync();

        Assert.Contains(profile.PositiveReasons, r => r.Message == "32 GB de RAM.");
        Assert.Contains(profile.MissingData, r => r.Message == "No se pudo determinar si el equipo tiene una GPU dedicada.");
    }

    [Fact]
    public async Task RefreshAsync_WhenBatterySourceThrows_ReportsUnknownBattery()
    {
        var (service, _, _, _, battery, _) = CreateService();
        battery.HasBatteryFactory = () => throw new UnauthorizedAccessException("simulated missing privileges");

        var profile = await service.RefreshAsync();

        Assert.Contains(profile.MissingData, r => r.Message == "No se pudo determinar si el equipo tiene batería.");
    }

    [Fact]
    public async Task RefreshAsync_WithMultipleGpus_PrefersTheDedicatedAdapterWithMostMemory()
    {
        var (service, _, _, graphics, _, _) = CreateService();
        var integrated = new GraphicsCapability("Integrated Graphics", false, null, null);
        var smallDedicated = new GraphicsCapability("Small Dedicated GPU", true, 2L * 1024 * 1024 * 1024, null);
        var largeDedicated = new GraphicsCapability("Large Dedicated GPU", true, 12L * 1024 * 1024 * 1024, null);
        graphics.AdaptersFactory = () => new[] { integrated, smallDedicated, largeDedicated };

        var profile = await service.RefreshAsync();

        Assert.Contains(profile.PositiveReasons, r => r.Message == "12 GB de memoria gráfica dedicada.");
    }

    [Fact]
    public async Task GetCachedProfile_AfterRefresh_DoesNotQuerySourcesAgain()
    {
        var (service, processor, memory, graphics, battery, windowsVersion) = CreateService();
        await service.RefreshAsync();

        var callCountsBefore = (processor.CallCount, memory.CallCount, graphics.CallCount, battery.CallCount, windowsVersion.CallCount);
        _ = service.GetCachedProfile();
        var callCountsAfter = (processor.CallCount, memory.CallCount, graphics.CallCount, battery.CallCount, windowsVersion.CallCount);

        Assert.Equal(callCountsBefore, callCountsAfter);
    }

    [Fact]
    public void GetCachedProfile_BeforeAnyRefresh_ReportsUnknownRatherThanBasic()
    {
        var (service, _, _, _, _, _) = CreateService();

        var profile = service.GetCachedProfile();

        Assert.False(profile.Snapshot.WasCached);
        Assert.Equal(HardwareDataConfidence.Unknown, profile.OverallConfidence);
    }

    [Fact]
    public async Task GetCachedProfile_AfterRefresh_ReflectsTheCapturedData()
    {
        var (service, processor, _, _, _, _) = CreateService();
        processor.LogicalProcessorCountFactory = () => 24;

        var refreshed = await service.RefreshAsync();
        var cached = service.GetCachedProfile();

        Assert.True(cached.Snapshot.WasCached);
        Assert.Equal(refreshed.Tier, cached.Tier);
    }

    [Fact]
    public async Task RefreshAsync_CalledAgain_UpdatesTheCache()
    {
        var (service, processor, _, _, _, _) = CreateService();
        processor.LogicalProcessorCountFactory = () => 4;
        var first = await service.RefreshAsync();

        processor.LogicalProcessorCountFactory = () => 24;
        var second = await service.RefreshAsync();

        Assert.NotEqual(first.Tier, second.Tier);
        Assert.Equal(second.Tier, service.GetCachedProfile().Tier);
    }

    [Fact]
    public async Task RefreshAsync_WithAlreadyCancelledToken_ThrowsAndDoesNotTouchTheCache()
    {
        var (service, processor, _, _, _, _) = CreateService();
        processor.LogicalProcessorCountFactory = () => 8;
        var first = await service.RefreshAsync();

        processor.LogicalProcessorCountFactory = () => 32;
        using var cancelledSource = new CancellationTokenSource();
        cancelledSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RefreshAsync(cancelledSource.Token));

        var afterCancellation = service.GetCachedProfile();
        Assert.Equal(first.Tier, afterCancellation.Tier);
    }

    [Fact]
    public async Task RefreshAsync_WhenEverySourceThrows_StillReturnsAProfileInsteadOfCrashing()
    {
        var (service, processor, memory, graphics, battery, windowsVersion) = CreateService();
        processor.NameFactory = () => throw new InvalidOperationException();
        processor.VendorFactory = () => throw new InvalidOperationException();
        processor.LogicalProcessorCountFactory = () => throw new InvalidOperationException();
        processor.PhysicalCoreCountFactory = () => throw new InvalidOperationException();
        memory.TotalPhysicalBytesFactory = () => throw new InvalidOperationException();
        graphics.AdaptersFactory = () => throw new InvalidOperationException();
        battery.HasBatteryFactory = () => throw new InvalidOperationException();
        windowsVersion.EditionFactory = () => throw new InvalidOperationException();
        windowsVersion.VersionFactory = () => throw new InvalidOperationException();

        var exception = await Record.ExceptionAsync(() => service.RefreshAsync());

        Assert.Null(exception);
        var profile = service.GetCachedProfile();
        Assert.Equal(HardwareDataConfidence.Unknown, profile.OverallConfidence);
    }

    private static (
        WindowsHardwareCapabilityService Service,
        FakeProcessorInfoSource Processor,
        FakeMemoryInfoSource Memory,
        FakeGraphicsInfoSource Graphics,
        FakeBatteryInfoSource Battery,
        FakeWindowsVersionInfoSource WindowsVersion) CreateService()
    {
        var processor = new FakeProcessorInfoSource();
        var memory = new FakeMemoryInfoSource();
        var graphics = new FakeGraphicsInfoSource();
        var battery = new FakeBatteryInfoSource();
        var windowsVersion = new FakeWindowsVersionInfoSource();

        var service = new WindowsHardwareCapabilityService(processor, memory, graphics, battery, windowsVersion);
        return (service, processor, memory, graphics, battery, windowsVersion);
    }
}
