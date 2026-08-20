namespace Nexo.Core.Commands.CommandCenter;

/// <summary>
/// Si un comando puede ejecutarse ahora mismo y, cuando no puede, por qué.
///
/// El motivo es obligatorio en el caso no disponible: el encargo exige que un comando no
/// disponible se muestre deshabilitado con una explicación clara, en vez de aparecer como si
/// funcionara y fallar al pulsarlo.
/// </summary>
public sealed record SakuraCommandAvailability
{
    private SakuraCommandAvailability(bool isAvailable, string? unavailableReason)
    {
        IsAvailable = isAvailable;
        UnavailableReason = unavailableReason;
    }

    public bool IsAvailable { get; }

    /// <summary>Motivo legible por una persona; <c>null</c> cuando el comando está disponible.</summary>
    public string? UnavailableReason { get; }

    public static SakuraCommandAvailability Available { get; } = new(true, null);

    public static SakuraCommandAvailability Unavailable(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Un comando no disponible debe explicar por qué.",
                nameof(reason));
        }

        return new SakuraCommandAvailability(false, reason.Trim());
    }
}
