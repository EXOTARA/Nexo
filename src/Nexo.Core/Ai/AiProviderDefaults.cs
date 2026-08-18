namespace Nexo.Core.Ai;

public static class AiProviderDefaults
{
    /// <summary>
    /// El orden en que se ofrecen en la interfaz: primero lo que no cuesta nada ni pide cuenta,
    /// después lo gratuito con clave, y al final lo de pago y lo manual. Un equipo modesto debería
    /// llegar a una opción que le sirva antes de leer la palabra "API".
    /// </summary>
    public static IReadOnlyList<AiProviderKind> SelectableOrder { get; } =
    [
        AiProviderKind.KohanaLocal,
        AiProviderKind.Groq,
        AiProviderKind.Gemini,
        AiProviderKind.OpenRouter,
        AiProviderKind.Anthropic,
        AiProviderKind.OpenAI,
        AiProviderKind.Ollama,
        AiProviderKind.LMStudio,
        AiProviderKind.OpenAICompatible,
        AiProviderKind.Disabled
    ];

    public static AiProviderPreset Get(AiProviderKind provider)
    {
        return provider switch
        {
            AiProviderKind.KohanaLocal => new AiProviderPreset(
                "IA local de Kohana",
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                "qwen3.5:4b",
                string.Empty,
                RequiresApiKey: false,
                AiProviderLocation.Local,
                AiProviderCost.FreeOnDevice,
                "Kohana descarga e inicia el motor por ti. Nada sale de este equipo, pero pide un equipo con holgura."),

            AiProviderKind.Ollama => new AiProviderPreset(
                "Ollama",
                OllamaRuntimeEndpoints.ExternalBaseUrl,
                string.Empty,
                string.Empty,
                RequiresApiKey: false,
                AiProviderLocation.Local,
                AiProviderCost.FreeOnDevice,
                "Usa una instalación propia de Ollama. Tienes que iniciarla tú; Kohana no la administra."),

            AiProviderKind.Groq => new AiProviderPreset(
                "Groq",
                "https://api.groq.com/openai/v1",
                // Diseño D60 — Groq retiró llama-3.3-70b-versatile el 17 de junio de 2026 para las
                // capas gratuita y de desarrollador, que son justo las que usa quien viene aquí por
                // «gratis y sin tarjeta». La respuesta pasaba a ser un 404 diciendo que el modelo no
                // existe «o no tienes acceso», que suena a problema de la clave y no a un modelo
                // apagado. Es el segundo caso igual tras el de Gemini en D39.
                //
                // openai/gpt-oss-120b es el reemplazo que recomienda Groq para ese modelo.
                "openai/gpt-oss-120b",
                "GROQ_API_KEY",
                RequiresApiKey: true,
                AiProviderLocation.Cloud,
                AiProviderCost.FreeTier,
                "Rápido y con capa gratuita sin tarjeta. La mejor opción si el equipo va justo.",
                "https://console.groq.com/keys"),

            // Diseño D39 — el alias «latest» y no una versión concreta. Estaba puesto
            // «gemini-2.0-flash» y Google lo apagó: la respuesta pasó a ser «this model is no
            // longer available» y Gemini dejó de funcionar sin que nadie tocara nada. Fijar una
            // versión en el código garantiza que el valor por defecto caduque solo; el alias sigue
            // al modelo vigente. Comprobado contra la API: responde en 845 ms, y varias versiones
            // concretas que la propia API lista devuelven 404 al usarlas.
            AiProviderKind.Gemini => new AiProviderPreset(
                "Google Gemini",
                "https://generativelanguage.googleapis.com/v1beta/openai",
                "gemini-flash-lite-latest",
                "GEMINI_API_KEY",
                RequiresApiKey: true,
                AiProviderLocation.Cloud,
                AiProviderCost.FreeTier,
                "Capa gratuita con una cuenta de Google.",
                "https://aistudio.google.com/apikey"),

            AiProviderKind.OpenRouter => new AiProviderPreset(
                "OpenRouter",
                "https://openrouter.ai/api/v1",
                "meta-llama/llama-3.3-70b-instruct:free",
                "OPENROUTER_API_KEY",
                RequiresApiKey: true,
                AiProviderLocation.Cloud,
                AiProviderCost.FreeTier,
                "Un solo acceso a muchos modelos. Los que terminan en «:free» no cuestan nada.",
                "https://openrouter.ai/keys"),

            AiProviderKind.Anthropic => new AiProviderPreset(
                "Claude (Anthropic)",
                "https://api.anthropic.com/v1",
                "claude-sonnet-5",
                "ANTHROPIC_API_KEY",
                RequiresApiKey: true,
                AiProviderLocation.Cloud,
                AiProviderCost.Paid,
                "Se paga por uso desde el primer mensaje: no tiene capa gratuita.",
                "https://console.anthropic.com/settings/keys"),

            AiProviderKind.OpenAI => new AiProviderPreset(
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5-mini",
                "OPENAI_API_KEY",
                RequiresApiKey: true,
                AiProviderLocation.Cloud,
                AiProviderCost.Paid,
                "Se paga por uso desde el primer mensaje: no tiene capa gratuita.",
                "https://platform.openai.com/api-keys"),

            AiProviderKind.LMStudio => new AiProviderPreset(
                "LM Studio",
                "http://127.0.0.1:1234/v1",
                string.Empty,
                string.Empty,
                RequiresApiKey: false,
                AiProviderLocation.Local,
                AiProviderCost.FreeOnDevice,
                "Usa el servidor local de LM Studio. Tienes que iniciarlo tú."),

            AiProviderKind.OpenAICompatible => new AiProviderPreset(
                "Otro compatible con OpenAI",
                "http://127.0.0.1:1234/v1",
                string.Empty,
                string.Empty,
                RequiresApiKey: false,
                AiProviderLocation.Cloud,
                AiProviderCost.None,
                "Para cualquier servicio que hable el protocolo de OpenAI. Tú pones la dirección."),

            _ => new AiProviderPreset(
                "Sin inteligencia artificial",
                string.Empty,
                string.Empty,
                string.Empty,
                RequiresApiKey: false,
                AiProviderLocation.None,
                AiProviderCost.None,
                "Kohana sigue funcionando: tareas, enfoque, rutinas y atajos no necesitan IA.")
        };
    }

    /// <summary>
    /// Los proveedores que hablan el protocolo nativo de Ollama en vez del de OpenAI. Reemplaza a la
    /// comparación suelta contra <see cref="AiProviderKind.Ollama"/> que había repartida por el
    /// código: al añadir <see cref="AiProviderKind.KohanaLocal"/>, cada una de esas comparaciones se
    /// habría vuelto silenciosamente incorrecta.
    /// </summary>
    public static bool UsesOllamaProtocol(AiProviderKind provider) =>
        provider is AiProviderKind.Ollama or AiProviderKind.KohanaLocal;

    /// <summary>true si Kohana misma instala, inicia y detiene el motor.</summary>
    public static bool IsManagedByKohana(AiProviderKind provider) =>
        provider == AiProviderKind.KohanaLocal;

    public static bool IsCloud(AiProviderKind provider) =>
        Get(provider).Location == AiProviderLocation.Cloud;

    public static string NormalizeBaseUrl(string? baseUrl)
    {
        return (baseUrl ?? string.Empty).Trim().TrimEnd('/');
    }
}
