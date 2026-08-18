namespace Nexo.Core.SelfCheck;

public enum SelfCheckStatus
{
    Correcto,

    /// <summary>Funciona pero hay algo que conviene mirar.</summary>
    Aviso,

    /// <summary>No funciona. Esto es lo que hay que arreglar antes de nada.</summary>
    Fallo,

    /// <summary>No se pudo comprobar en este equipo. **No es lo mismo que "correcto"**.</summary>
    NoComprobado
}

/// <summary>
/// Diseño D22 — el resultado de una comprobación. <see cref="WhatItProves"/> no es decoración: una
/// lista de verdes sin decir qué demuestra cada uno da una sensación de seguridad que no se ha
/// ganado. Si alguien no puede leer qué se probó, el informe no vale para decidir nada.
/// </summary>
public sealed record SelfCheckResult(
    string Id,
    string Title,
    SelfCheckStatus Status,
    string WhatItProves,
    string Detail)
{
    public static SelfCheckResult Ok(string id, string title, string proves, string detail = "Correcto.") =>
        new(id, title, SelfCheckStatus.Correcto, proves, detail);

    public static SelfCheckResult Warn(string id, string title, string proves, string detail) =>
        new(id, title, SelfCheckStatus.Aviso, proves, detail);

    public static SelfCheckResult Fail(string id, string title, string proves, string detail) =>
        new(id, title, SelfCheckStatus.Fallo, proves, detail);

    public static SelfCheckResult Skipped(string id, string title, string proves, string detail) =>
        new(id, title, SelfCheckStatus.NoComprobado, proves, detail);
}
