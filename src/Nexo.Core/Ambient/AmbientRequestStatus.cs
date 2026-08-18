namespace Nexo.Core.Ambient;

public enum AmbientRequestStatus
{
    Listening,
    Thinking,

    /// <summary>
    /// Diseño D7 — la respuesta está llegando por partes y ya se puede mostrar. Estado propio, no
    /// una variante de <see cref="Thinking"/>: quien presenta necesita distinguir "todavía no hay
    /// nada que enseñar" de "esto que se ve aún no está completo".
    /// </summary>
    Streaming,

    Result,
    Cancelled,
    Failed
}
