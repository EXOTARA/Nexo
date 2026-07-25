namespace Nexo.Core.Hardware;

public sealed record GraphicsCapability(
    string? Name,
    bool? IsDedicated,
    long? DedicatedMemoryBytes,
    long? SharedMemoryBytes)
{
    public static GraphicsCapability Unknown { get; } = new(
        Name: null,
        IsDedicated: null,
        DedicatedMemoryBytes: null,
        SharedMemoryBytes: null);
}
