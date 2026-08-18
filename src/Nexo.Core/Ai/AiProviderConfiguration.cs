namespace Nexo.Core.Ai;

public sealed record AiProviderConfiguration(
    AiProviderKind Provider,
    string BaseUrl,
    string Model,
    string ApiKeyEnvironmentVariable)
{
    /// <summary>
    /// La clave que la persona escribió en Kohana, ya descifrada. Vive solo en memoria y solo el
    /// tiempo que dura la petición: el disco guarda la versión cifrada con DPAPI, y
    /// <c>settings.json</c> no la ve nunca.
    ///
    /// Existe porque exigir una variable de entorno era una barrera real. Alguien que no programa no
    /// va a abrir las variables de entorno de Windows para probar Kohana, y sin eso las opciones de
    /// nube —justo las que salvan a un equipo sin recursos— quedaban fuera de su alcance.
    /// La variable de entorno se conserva como alternativa para quien ya la tenía puesta.
    /// </summary>
    public string? StoredApiKey { get; init; }

    public bool IsEnabled => Provider != AiProviderKind.Disabled;

    public string DisplayName => AiProviderDefaults.Get(Provider).DisplayName;

    public bool RequiresApiKey => AiProviderDefaults.Get(Provider).RequiresApiKey;

    public string? ReadApiKey()
    {
        if (!string.IsNullOrWhiteSpace(StoredApiKey))
        {
            return StoredApiKey.Trim();
        }

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
