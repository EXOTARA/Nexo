namespace Nexo.Core.Ai;

public static class AiProviderDefaults
{
    public static AiProviderPreset Get(AiProviderKind provider)
    {
        return provider switch
        {
            AiProviderKind.OpenAI => new AiProviderPreset(
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5-mini",
                "OPENAI_API_KEY",
                RequiresApiKey: true),

            AiProviderKind.Ollama => new AiProviderPreset(
                "Ollama",
                "http://localhost:11434/v1",
                string.Empty,
                string.Empty,
                RequiresApiKey: false),

            AiProviderKind.LMStudio => new AiProviderPreset(
                "LM Studio",
                "http://localhost:1234/v1",
                string.Empty,
                string.Empty,
                RequiresApiKey: false),

            AiProviderKind.OpenAICompatible => new AiProviderPreset(
                "Compatible con OpenAI",
                "http://localhost:1234/v1",
                string.Empty,
                string.Empty,
                RequiresApiKey: false),

            _ => new AiProviderPreset(
                "Desactivada",
                string.Empty,
                string.Empty,
                string.Empty,
                RequiresApiKey: false)
        };
    }

    public static string NormalizeBaseUrl(string? baseUrl)
    {
        return (baseUrl ?? string.Empty).Trim().TrimEnd('/');
    }
}
