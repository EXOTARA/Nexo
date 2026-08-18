using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Optimization;
using Nexo.Core.Settings;

namespace Nexo.App.Optimization;

/// <summary>
/// Diseño D11 (Fase 4) — aplica el modo de rendimiento propio de Kohana escribiendo en las
/// preferencias y volviendo a calcular el plan de motores.
///
/// Vive en la capa de aplicación porque es la única que posee las preferencias vivas; el
/// coordinador solo conoce la interfaz. Y se verifica igual que el plan de energía: se reescribe y
/// se RELEE. Aquí releer es casi trivial, pero la regla es la misma para los dos objetivos a
/// propósito — en cuanto un objetivo se dé por aplicado sin comprobarlo, la reversión deja de estar
/// verificada y vuelve a ser una promesa.
/// </summary>
public sealed class PreferencesKohanaFootprintApplier : IKohanaFootprintApplier
{
    private readonly ShellPreferences _preferences;
    private readonly Action _onModeChanged;

    public PreferencesKohanaFootprintApplier(ShellPreferences preferences, Action onModeChanged)
    {
        _preferences = preferences;
        _onModeChanged = onModeChanged;
    }

    public HardwarePerformanceMode ReadCurrentMode() => _preferences.HardwarePerformanceMode;

    public OptimizationStepResult ApplyMode(HardwarePerformanceMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            return OptimizationStepResult.Failed("Ese modo de rendimiento no existe.");
        }

        _preferences.HardwarePerformanceMode = mode;
        _onModeChanged();

        return _preferences.HardwarePerformanceMode == mode
            ? OptimizationStepResult.Ok($"Kohana quedó en modo {mode}.")
            : OptimizationStepResult.Failed(
                "El modo de rendimiento de Kohana no quedó guardado, así que no lo doy por hecho.");
    }
}
