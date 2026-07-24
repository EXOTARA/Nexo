namespace Nexo.Core.Hardware;

public sealed record MemoryCapability(ulong? TotalPhysicalBytes)
{
    public static MemoryCapability Unknown { get; } = new(TotalPhysicalBytes: null);
}
