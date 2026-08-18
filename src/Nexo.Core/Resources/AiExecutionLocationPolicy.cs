using Nexo.Core.Ai;

namespace Nexo.Core.Resources;

public static class AiExecutionLocationPolicy
{
    public static bool UsesLocalRuntime(AiProviderConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (AiProviderDefaults.UsesOllamaProtocol(configuration.Provider) ||
            configuration.Provider == AiProviderKind.LMStudio)
        {
            return true;
        }

        return configuration.Provider == AiProviderKind.OpenAICompatible &&
               Uri.TryCreate(configuration.BaseUrl, UriKind.Absolute, out var uri) &&
               uri.IsLoopback;
    }
}
