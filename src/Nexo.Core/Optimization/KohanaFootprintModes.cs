using Nexo.Core.AdaptiveEngine;

namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D11 — traduce el id de un cambio al modo de rendimiento propio de Kohana. La tabla vive
/// en un solo sitio para que el planificador (que propone ids) y el coordinador (que los aplica) no
/// puedan discrepar.
/// </summary>
public static class KohanaFootprintModes
{
    public const string Eco = "kohana.eco";

    public const string Balanced = "kohana.balanced";

    public const string Automatic = "kohana.automatic";

    public static HardwarePerformanceMode? Resolve(string? changeId) => changeId switch
    {
        Eco => HardwarePerformanceMode.Eco,
        Balanced => HardwarePerformanceMode.Balanced,
        Automatic => HardwarePerformanceMode.Automatic,
        _ => null
    };
}
