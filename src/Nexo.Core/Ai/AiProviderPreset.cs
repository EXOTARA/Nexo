namespace Nexo.Core.Ai;

public sealed record AiProviderPreset(
    string DisplayName,
    string BaseUrl,
    string DefaultModel,
    string ApiKeyEnvironmentVariable,
    bool RequiresApiKey);
