namespace Nexo.Core.Ai;

public sealed record AiProviderConfiguration(
    AiProviderKind Provider,
    string BaseUrl,
    string Model,
    string ApiKeyEnvironmentVariable)
{
    public bool IsEnabled => Provider != AiProviderKind.Disabled;

    public string DisplayName => AiProviderDefaults.Get(Provider).DisplayName;

    public bool RequiresApiKey => AiProviderDefaults.Get(Provider).RequiresApiKey;

    public string? ReadApiKey()
    {
        if (string.IsNullOrWhiteSpace(ApiKeyEnvironmentVariable))
        {
            return null;
        }

        return Environment.GetEnvironmentVariable(
            ApiKeyEnvironmentVariable.Trim(),
            EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable(
                ApiKeyEnvironmentVariable.Trim(),
                EnvironmentVariableTarget.User);
    }
}
