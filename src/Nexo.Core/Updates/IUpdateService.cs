namespace Nexo.Core.Updates;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default);
}
