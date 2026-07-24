namespace Nexo.Core.Hardware;

public sealed record ProcessorCapability(
    string? Name,
    string? Vendor,
    int? PhysicalCores,
    int? LogicalProcessors,
    string? OperatingSystemArchitecture,
    string? ProcessArchitecture)
{
    public static ProcessorCapability Unknown { get; } = new(
        Name: null,
        Vendor: null,
        PhysicalCores: null,
        LogicalProcessors: null,
        OperatingSystemArchitecture: null,
        ProcessArchitecture: null);
}
