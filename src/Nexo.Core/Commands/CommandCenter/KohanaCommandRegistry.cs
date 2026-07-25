namespace Nexo.Core.Commands.CommandCenter;

/// <summary>
/// Registro de los comandos disponibles en el Sakura Command Center.
///
/// Existe para que las acciones dejen de estar incrustadas dentro de una ventana: se registran
/// una sola vez al construir el shell y a partir de ahí se consultan y buscan sin reflexión y sin
/// construir vistas.
/// </summary>
public sealed class KohanaCommandRegistry
{
    private readonly Dictionary<string, KohanaCommandDescriptor> _commandsById =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<KohanaCommandDescriptor> _ordered = [];

    /// <summary>Comandos en el orden en que se registraron; el orden de registro es el desempate estable.</summary>
    public IReadOnlyList<KohanaCommandDescriptor> Commands => _ordered;

    public int Count => _ordered.Count;

    /// <summary>
    /// Registra un comando. El identificador debe ser único: registrar dos veces el mismo Id es un
    /// error de programación (dos acciones distintas respondiendo al mismo identificador estable),
    /// no algo que deba resolverse en silencio.
    /// </summary>
    public void Register(KohanaCommandDescriptor command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_commandsById.TryAdd(command.Id, command))
        {
            throw new InvalidOperationException(
                $"Ya hay un comando registrado con el identificador '{command.Id}'.");
        }

        _ordered.Add(command);
    }

    public void RegisterRange(IEnumerable<KohanaCommandDescriptor> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        foreach (var command in commands)
        {
            Register(command);
        }
    }

    public KohanaCommandDescriptor? Find(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : _commandsById.GetValueOrDefault(id.Trim());

    public bool Contains(string? id) => Find(id) is not null;
}
