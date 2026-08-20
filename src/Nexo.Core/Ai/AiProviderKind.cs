namespace Nexo.Core.Ai;

/// <summary>
/// Los valores se serializan como número en <c>settings.json</c>, así que el orden es parte del
/// formato de archivo: los nuevos proveedores se añaden **al final** y nunca se reordenan. Renumerar
/// esto convertiría el "Ollama" de alguien en "Groq" al abrir la aplicación.
/// </summary>
public enum AiProviderKind
{
    Disabled = 0,
    OpenAI = 1,

    /// <summary>
    /// Ollama que la persona instaló por su cuenta, escuchando en el puerto 11434.
    /// No es el motor que Sakura administra — ese es <see cref="SakuraLocal"/>.
    /// </summary>
    Ollama = 2,

    LMStudio = 3,
    OpenAICompatible = 4,

    /// <summary>
    /// El motor local que Sakura descarga, inicia y detiene sola, en el puerto 11435. Separado de
    /// <see cref="Ollama"/> porque confundirlos es justo el defecto que este diseño corrige: elegir
    /// "Ollama" escribía la dirección del externo (11434) y dejaba el motor administrado (11435)
    /// instalado pero inalcanzable, con el modelo ya descargado y ninguna forma de usarlo.
    /// </summary>
    SakuraLocal = 5,

    Anthropic = 6,
    Groq = 7,
    Gemini = 8,
    OpenRouter = 9
}
