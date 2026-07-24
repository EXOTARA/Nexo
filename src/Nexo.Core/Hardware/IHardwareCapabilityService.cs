namespace Nexo.Core.Hardware;

public interface IHardwareCapabilityService
{
    HardwareCapabilityProfile GetCachedProfile();

    Task<HardwareCapabilityProfile> RefreshAsync(CancellationToken cancellationToken = default);
}
