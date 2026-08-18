namespace Nexo.Core.Ai;

/// <summary>
/// Guarda una clave por proveedor, no una sola global: cambiar de Groq a Gemini y volver no debería
/// obligar a pegar la clave otra vez.
/// </summary>
public interface IAiApiKeyStore
{
    string? Read(AiProviderKind provider);

    void Write(AiProviderKind provider, string apiKey);

    void Remove(AiProviderKind provider);
}
