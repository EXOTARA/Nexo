namespace Nexo.Core.Flow;

/// <summary>
/// Diseño D6.2 — por qué no se insertó el texto dictado. Se distingue el motivo en vez de devolver
/// un simple booleano porque cada caso merece una respuesta distinta de la capa de aplicación: un
/// cambio de foco pide copiar al portapapeles y avisar; una ventana sensible pide NO copiar nada.
/// </summary>
public enum FlowInsertionFailure
{
    None,

    /// <summary>El dictado no produjo texto (silencio, o solo muletillas).</summary>
    EmptyText,

    /// <summary>No se recordó ninguna ventana destino al empezar a dictar.</summary>
    NoTargetWindow,

    /// <summary>
    /// La ventana en primer plano ya no es la de cuando empezó el dictado. Es el riesgo que el
    /// roadmap señala explícitamente para esta fase: escribir en el lugar equivocado.
    /// </summary>
    FocusChanged,

    /// <summary>La ventana destino está marcada como sensible (gestor de contraseñas, UAC…).</summary>
    SensitiveWindow,

    /// <summary>Windows rechazó la inyección de teclado.</summary>
    SendFailed
}

public sealed record FlowInsertionResult(
    bool IsInserted,
    string Detail,
    FlowInsertionFailure Failure = FlowInsertionFailure.None)
{
    public static FlowInsertionResult Inserted(int characterCount) =>
        new(true, $"Escribí {characterCount} caracteres en la ventana activa.");

    public static FlowInsertionResult Failed(FlowInsertionFailure failure, string detail) =>
        new(false, detail, failure);
}
