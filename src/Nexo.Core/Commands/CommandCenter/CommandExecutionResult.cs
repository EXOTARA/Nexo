namespace Nexo.Core.Commands.CommandCenter;

/// <summary>
/// Resultado de ejecutar un comando. Nunca se informa éxito cuando algo falló, y un fallo nunca
/// se propaga como excepción hacia la UI: se devuelve descrito, para que el shell pueda avisar de
/// forma no modal y seguir funcionando.
/// </summary>
public sealed record CommandExecutionResult
{
    private CommandExecutionResult(bool succeeded, string? message, Exception? error)
    {
        Succeeded = succeeded;
        Message = message;
        Error = error;
    }

    public bool Succeeded { get; }

    /// <summary>Mensaje para la persona usuaria. Puede existir también en caso de éxito.</summary>
    public string? Message { get; }

    /// <summary>
    /// Excepción original cuando el fallo vino de una, conservada íntegra (tipo, mensaje, inner y
    /// stack trace) para el registro de diagnóstico. <c>null</c> cuando el comando simplemente no
    /// pudo aplicarse.
    /// </summary>
    public Exception? Error { get; }

    public static CommandExecutionResult Success(string? message = null) => new(true, message, null);

    /// <summary>El comando no pudo aplicarse, pero no hubo excepción (p. ej. no había nada que hacer).</summary>
    public static CommandExecutionResult Failure(string message) =>
        new(false, RequireMessage(message), null);

    public static CommandExecutionResult FromException(string message, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new CommandExecutionResult(false, RequireMessage(message), error);
    }

    private static string RequireMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "Un fallo debe explicar qué acción no se pudo completar.",
                nameof(message));
        }

        return message.Trim();
    }
}
