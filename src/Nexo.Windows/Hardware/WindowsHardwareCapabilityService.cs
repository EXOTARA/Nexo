using Nexo.Core.Hardware;

namespace Nexo.Windows.Hardware;

public sealed class WindowsHardwareCapabilityService : IHardwareCapabilityService
{
    private readonly IProcessorInfoSource _processorInfoSource;
    private readonly IMemoryInfoSource _memoryInfoSource;
    private readonly IGraphicsInfoSource _graphicsInfoSource;
    private readonly IBatteryInfoSource _batteryInfoSource;
    private readonly IWindowsVersionInfoSource _windowsVersionInfoSource;

    private readonly object _lock = new();
    private HardwareCapabilitySnapshot _cachedSnapshot = HardwareCapabilitySnapshot.Empty;
    private bool _hasCaptured;

    public WindowsHardwareCapabilityService()
        : this(
            new RegistryProcessorInfoSource(),
            new WinApiMemoryInfoSource(),
            new RegistryGraphicsInfoSource(),
            new WinApiBatteryInfoSource(),
            new RegistryWindowsVersionInfoSource())
    {
    }

    public WindowsHardwareCapabilityService(
        IProcessorInfoSource processorInfoSource,
        IMemoryInfoSource memoryInfoSource,
        IGraphicsInfoSource graphicsInfoSource,
        IBatteryInfoSource batteryInfoSource,
        IWindowsVersionInfoSource windowsVersionInfoSource)
    {
        _processorInfoSource = processorInfoSource ?? throw new ArgumentNullException(nameof(processorInfoSource));
        _memoryInfoSource = memoryInfoSource ?? throw new ArgumentNullException(nameof(memoryInfoSource));
        _graphicsInfoSource = graphicsInfoSource ?? throw new ArgumentNullException(nameof(graphicsInfoSource));
        _batteryInfoSource = batteryInfoSource ?? throw new ArgumentNullException(nameof(batteryInfoSource));
        _windowsVersionInfoSource = windowsVersionInfoSource ?? throw new ArgumentNullException(nameof(windowsVersionInfoSource));
    }

    public HardwareCapabilityProfile GetCachedProfile()
    {
        HardwareCapabilitySnapshot snapshot;
        lock (_lock)
        {
            snapshot = _cachedSnapshot with { WasCached = _hasCaptured };
        }

        return HardwareCapabilityPolicy.Evaluate(snapshot);
    }

    public async Task<HardwareCapabilityProfile> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await Task.Run(() => CaptureSnapshot(cancellationToken), cancellationToken)
            .ConfigureAwait(false);

        lock (_lock)
        {
            _cachedSnapshot = snapshot;
            _hasCaptured = true;
        }

        return HardwareCapabilityPolicy.Evaluate(snapshot with { WasCached = false });
    }

    private HardwareCapabilitySnapshot CaptureSnapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var processor = DetectProcessor();

        cancellationToken.ThrowIfCancellationRequested();
        var memory = DetectMemory();

        cancellationToken.ThrowIfCancellationRequested();
        var graphicsAdapters = DetectGraphicsAdapters();
        var preferredGraphicsAdapter = SelectPreferredGraphicsAdapter(graphicsAdapters);

        cancellationToken.ThrowIfCancellationRequested();
        var hasBattery = DetectBatteryPresence();

        cancellationToken.ThrowIfCancellationRequested();
        var edition = DetectWindowsEdition();
        var version = DetectWindowsVersion();

        return new HardwareCapabilitySnapshot(
            processor,
            memory,
            graphicsAdapters,
            preferredGraphicsAdapter,
            hasBattery,
            edition,
            version,
            CapturedAt: DateTimeOffset.Now,
            WasCached: false);
    }

    private ProcessorCapability DetectProcessor()
    {
        string? name = null;
        string? vendor = null;
        int? logicalProcessors = null;
        int? physicalCores = null;

        try
        {
            name = _processorInfoSource.ReadProcessorName();
        }
        catch (Exception)
        {
            // Degradación segura: el nombre del CPU queda desconocido.
        }

        try
        {
            vendor = _processorInfoSource.ReadProcessorVendor();
        }
        catch (Exception)
        {
            // Degradación segura: el fabricante del CPU queda desconocido.
        }

        try
        {
            logicalProcessors = _processorInfoSource.ReadLogicalProcessorCount();
        }
        catch (Exception)
        {
            // Degradación segura: la cantidad de procesadores lógicos queda desconocida.
        }

        try
        {
            physicalCores = _processorInfoSource.ReadPhysicalCoreCount();
        }
        catch (Exception)
        {
            // Degradación segura: la cantidad de núcleos físicos queda desconocida.
        }

        string? osArchitecture;
        string? processArchitecture;
        try
        {
            osArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
        }
        catch (Exception)
        {
            osArchitecture = null;
        }

        try
        {
            processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
        }
        catch (Exception)
        {
            processArchitecture = null;
        }

        return new ProcessorCapability(name, vendor, physicalCores, logicalProcessors, osArchitecture, processArchitecture);
    }

    private MemoryCapability DetectMemory()
    {
        try
        {
            return new MemoryCapability(_memoryInfoSource.ReadTotalPhysicalBytes());
        }
        catch (Exception)
        {
            return MemoryCapability.Unknown;
        }
    }

    private IReadOnlyList<GraphicsCapability> DetectGraphicsAdapters()
    {
        try
        {
            return _graphicsInfoSource.ReadAdapters();
        }
        catch (Exception)
        {
            return Array.Empty<GraphicsCapability>();
        }
    }

    private static GraphicsCapability SelectPreferredGraphicsAdapter(IReadOnlyList<GraphicsCapability> adapters)
    {
        if (adapters.Count == 0)
        {
            return GraphicsCapability.Unknown;
        }

        var preferredDedicated = adapters
            .Where(adapter => adapter.IsDedicated == true)
            .OrderByDescending(adapter => adapter.DedicatedMemoryBytes ?? -1)
            .FirstOrDefault();

        return preferredDedicated ?? adapters[0];
    }

    private bool? DetectBatteryPresence()
    {
        try
        {
            return _batteryInfoSource.ReadHasBattery();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string? DetectWindowsEdition()
    {
        try
        {
            return _windowsVersionInfoSource.ReadEdition();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string? DetectWindowsVersion()
    {
        try
        {
            return _windowsVersionInfoSource.ReadVersion();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
