using Nexo.Core.Hardware;
using Nexo.Windows.Hardware;

namespace Nexo.Windows.Tests.Hardware;

internal sealed class FakeProcessorInfoSource : IProcessorInfoSource
{
    public Func<string?> NameFactory { get; set; } = () => "Test CPU";
    public Func<string?> VendorFactory { get; set; } = () => "Test Vendor";
    public Func<int?> LogicalProcessorCountFactory { get; set; } = () => 8;
    public Func<int?> PhysicalCoreCountFactory { get; set; } = () => 4;
    public int CallCount { get; private set; }

    public string? ReadProcessorName()
    {
        CallCount++;
        return NameFactory();
    }

    public string? ReadProcessorVendor() => VendorFactory();

    public int? ReadLogicalProcessorCount() => LogicalProcessorCountFactory();

    public int? ReadPhysicalCoreCount() => PhysicalCoreCountFactory();
}

internal sealed class FakeMemoryInfoSource : IMemoryInfoSource
{
    public Func<ulong?> TotalPhysicalBytesFactory { get; set; } = () => 16UL * 1024 * 1024 * 1024;
    public int CallCount { get; private set; }

    public ulong? ReadTotalPhysicalBytes()
    {
        CallCount++;
        return TotalPhysicalBytesFactory();
    }
}

internal sealed class FakeGraphicsInfoSource : IGraphicsInfoSource
{
    public Func<IReadOnlyList<GraphicsCapability>> AdaptersFactory { get; set; } =
        () => new[] { new GraphicsCapability("Test GPU", true, 4L * 1024 * 1024 * 1024, null) };

    public int CallCount { get; private set; }

    public IReadOnlyList<GraphicsCapability> ReadAdapters()
    {
        CallCount++;
        return AdaptersFactory();
    }
}

internal sealed class FakeBatteryInfoSource : IBatteryInfoSource
{
    public Func<bool?> HasBatteryFactory { get; set; } = () => false;
    public int CallCount { get; private set; }

    public bool? ReadHasBattery()
    {
        CallCount++;
        return HasBatteryFactory();
    }
}

internal sealed class FakeWindowsVersionInfoSource : IWindowsVersionInfoSource
{
    public Func<string?> EditionFactory { get; set; } = () => "Test Windows Edition";
    public Func<string?> VersionFactory { get; set; } = () => "10.0";
    public int CallCount { get; private set; }

    public string? ReadEdition()
    {
        CallCount++;
        return EditionFactory();
    }

    public string? ReadVersion() => VersionFactory();
}
