namespace Nexo.Core.Hardware;

public sealed record HardwareCapabilitySnapshot(
    ProcessorCapability Processor,
    MemoryCapability Memory,
    IReadOnlyList<GraphicsCapability> GraphicsAdapters,
    GraphicsCapability PreferredGraphicsAdapter,
    bool? HasBattery,
    string? WindowsEdition,
    string? WindowsVersion,
    DateTimeOffset CapturedAt,
    bool WasCached)
{
    public static HardwareCapabilitySnapshot Empty { get; } = new(
        Processor: ProcessorCapability.Unknown,
        Memory: MemoryCapability.Unknown,
        GraphicsAdapters: Array.Empty<GraphicsCapability>(),
        PreferredGraphicsAdapter: GraphicsCapability.Unknown,
        HasBattery: null,
        WindowsEdition: null,
        WindowsVersion: null,
        CapturedAt: DateTimeOffset.MinValue,
        WasCached: false);
}
