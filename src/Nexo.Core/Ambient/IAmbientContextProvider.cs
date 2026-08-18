namespace Nexo.Core.Ambient;

/// <summary>
/// Diseño D4 — captura metadatos mínimos (no contenido) de una ventana dada por su handle nativo,
/// para adjuntarlos a una solicitud ambiental como <see cref="AmbientContextSnapshot"/>. Separado
/// de <c>IScreenCaptureService</c> (Nexo.Core.Vision, Fase 2) a propósito: D4 nunca captura píxeles
/// ni texto de pantalla, solo título y proceso, y solo cuando la ventana no está marcada sensible.
/// </summary>
public interface IAmbientContextProvider
{
    AmbientContextSnapshot? Capture(long windowHandle);
}
