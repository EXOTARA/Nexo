namespace Nexo.Core.Ai;

/// <summary>
/// Dónde se ejecuta de verdad el modelo. Es lo primero que alguien necesita saber antes de elegir:
/// decide si su equipo va a sudar o no, y si el texto sale de la máquina.
/// </summary>
public enum AiProviderLocation
{
    None,
    Local,
    Cloud
}

/// <summary>
/// Qué cuesta usar el proveedor. Se declara aquí, con el resto del preset, para que la interfaz no
/// tenga que llevar su propia tabla paralela que se desactualice.
/// </summary>
public enum AiProviderCost
{
    None,

    /// <summary>Gratis porque corre en el equipo: no hay cuenta ni clave.</summary>
    FreeOnDevice,

    /// <summary>Tiene una capa gratuita real y utilizable sin tarjeta.</summary>
    FreeTier,

    /// <summary>Se paga por uso desde el primer mensaje.</summary>
    Paid
}

public sealed record AiProviderPreset(
    string DisplayName,
    string BaseUrl,
    string DefaultModel,
    string ApiKeyEnvironmentVariable,
    bool RequiresApiKey,
    AiProviderLocation Location = AiProviderLocation.None,
    AiProviderCost Cost = AiProviderCost.None,
    string Summary = "",
    string? ApiKeyUrl = null);
