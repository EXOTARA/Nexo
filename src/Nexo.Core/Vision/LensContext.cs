using Nexo.Core.Ai;

namespace Nexo.Core.Vision;

/// <summary>Diseño D5.6 — lo que produce <see cref="LensContextBuilder"/>: listo para enviar.</summary>
public sealed record LensContext(
    string Prompt,
    string SystemContext,
    AiRequestMode RequestMode);
