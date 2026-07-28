namespace Nexo.Core.Ambient;

/// <summary>
/// Diseño D4 — metadatos mínimos de la ventana activa en el momento de la solicitud. Nunca
/// contenido visual ni texto de pantalla (eso es Lens, Fase 2). Título y proceso pueden venir
/// vacíos si la fuente no pudo resolverlos o si la ventana está marcada como sensible.
/// </summary>
public sealed record AmbientContextSnapshot(
    string? WindowTitle,
    string? ProcessName,
    bool IsSensitive);
