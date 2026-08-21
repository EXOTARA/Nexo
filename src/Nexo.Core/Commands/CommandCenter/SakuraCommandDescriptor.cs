namespace Nexo.Core.Commands.CommandCenter;

/// <summary>
/// Un comando del Sakura Command Center: qué es, cómo se busca, si puede ejecutarse ahora y qué
/// hace.
///
/// El descriptor no sabe nada de WPF. La ejecución es un delegado asíncrono que el shell
/// proporciona al registrar el comando, enlazándolo a un servicio real ya existente
/// (<c>FocusManager</c>, <c>IAudioMixerService</c>, navegación del shell…). Así el registro y la
/// búsqueda pueden probarse sin levantar interfaz.
/// </summary>
public sealed class SakuraCommandDescriptor
{
    private readonly Func<CancellationToken, Task<CommandExecutionResult>> _execute;
    private readonly Func<SakuraCommandAvailability>? _availability;

    public SakuraCommandDescriptor(
        string id,
        string title,
        string description,
        SakuraCommandCategory category,
        Func<CancellationToken, Task<CommandExecutionResult>> execute,
        IReadOnlyList<string>? keywords = null,
        string? iconKey = null,
        string? shortcut = null,
        Func<SakuraCommandAvailability>? availability = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Un comando necesita un identificador estable.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Un comando necesita un título visible.", nameof(title));
        }

        ArgumentNullException.ThrowIfNull(execute);

        Id = id.Trim();
        Title = title.Trim();
        Description = (description ?? string.Empty).Trim();
        Category = category;
        Keywords = keywords is null
            ? []
            : [.. keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => keyword.Trim())];
        IconKey = string.IsNullOrWhiteSpace(iconKey) ? null : iconKey.Trim();
        Shortcut = string.IsNullOrWhiteSpace(shortcut) ? null : shortcut.Trim();

        _execute = execute;
        _availability = availability;
    }

    /// <summary>Identificador estable; no cambia aunque cambie el título visible.</summary>
    public string Id { get; }

    public string Title { get; }

    public string Description { get; }

    public SakuraCommandCategory Category { get; }

    /// <summary>Alias y sinónimos por los que también debe encontrarse el comando.</summary>
    public IReadOnlyList<string> Keywords { get; }

    /// <summary>Clave de recurso del icono, resuelta por la capa de UI. El núcleo no lo interpreta.</summary>
    public string? IconKey { get; }

    /// <summary>Atajo visible (p. ej. "Alt + A") cuando el comando tiene uno; si no, <c>null</c>.</summary>
    public string? Shortcut { get; }

    /// <summary>
    /// Se consulta en el momento de mostrar o ejecutar, no al registrar: la disponibilidad de
    /// "silenciar audio" o "finalizar sesión de enfoque" depende del estado actual.
    /// </summary>
    public SakuraCommandAvailability GetAvailability()
    {
        if (_availability is null)
        {
            return SakuraCommandAvailability.Available;
        }

        try
        {
            return _availability() ?? SakuraCommandAvailability.Available;
        }
        catch (Exception exception)
        {
            // Consultar disponibilidad nunca debe tumbar la paleta: si el servicio subyacente
            // falla al responder, el comando se muestra deshabilitado explicando el motivo.
            return SakuraCommandAvailability.Unavailable(
                $"No se pudo comprobar si esta acción está disponible: {exception.Message}");
        }
    }

    /// <summary>
    /// Ejecuta el comando. No lanza: cualquier excepción se traduce a un
    /// <see cref="CommandExecutionResult"/> fallido conservando la excepción original para el
    /// registro. Un comando no disponible tampoco se ejecuta.
    /// </summary>
    public async Task<CommandExecutionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var availability = GetAvailability();
        if (!availability.IsAvailable)
        {
            return CommandExecutionResult.Failure(
                availability.UnavailableReason ?? "Esta acción no está disponible ahora mismo.");
        }

        try
        {
            return await _execute(cancellationToken).ConfigureAwait(false)
                   ?? CommandExecutionResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancelar no es un error: no se informa fallo ni se registra como incidente.
            return CommandExecutionResult.Failure($"«{Title}» se canceló.");
        }
        catch (Exception exception)
        {
            return CommandExecutionResult.FromException(
                $"No se pudo completar «{Title}».",
                exception);
        }
    }
}
