using System.Runtime.InteropServices;
using Nexo.Core.Optimization;

namespace Nexo.Windows.Optimization;

/// <summary>
/// Diseño D8 (Fase 4) — aplica los cambios del plan usando **exclusivamente API documentada de
/// Win32** (<c>PowerGetActiveScheme</c> / <c>PowerSetActiveScheme</c>, de powrprof.dll). Es una
/// decisión explícita registrada en el roadmap: nada de trucos de registro ni de lanzar procesos,
/// para no disparar falsos positivos de antivirus.
///
/// Diseño D11 — **cada cambio se verifica releyendo el estado**. Que <c>PowerSetActiveScheme</c>
/// devuelva 0 significa que Windows aceptó la llamada, no que el plan activo sea ahora el que se
/// pidió: una directiva de grupo o un fabricante pueden reponer el suyo. El criterio de terminado de
/// la fase pide reversión *verificada*, y verificar es releer. Sin esto, Kohana podía anunciar
/// "listo" sobre un cambio que no ocurrió, y ofrecer después deshacer algo que nunca se hizo.
/// </summary>
public sealed class WindowsOptimizationApplier : IOptimizationApplier
{
    // GUIDs de los planes de energía estándar de Windows (documentados y estables).
    private static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid PowerSaver = new("a1841308-3541-4fab-bc81-f71556f20b4a");

    public string? ReadCurrentPowerPlanId()
    {
        var pointer = IntPtr.Zero;
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out pointer) != 0 || pointer == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStructure<Guid>(pointer).ToString();
        }
        catch (Exception exception) when (
            exception is EntryPointNotFoundException or DllNotFoundException)
        {
            return null;
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                LocalFree(pointer);
            }
        }
    }

    public OptimizationStepResult ApplyPowerPlan(string changeId)
    {
        var scheme = ResolveScheme(changeId);
        return scheme is null
            ? OptimizationStepResult.Failed($"No reconozco el cambio «{changeId}», así que no lo apliqué.")
            : SetAndVerify(scheme.Value, "Windows no aceptó el cambio de plan de energía.");
    }

    public OptimizationStepResult RestorePowerPlan(string previousPowerPlanId) =>
        Guid.TryParse(previousPowerPlanId, out var previous)
            ? SetAndVerify(previous, "Windows no aceptó restaurar el plan de energía anterior.")
            : OptimizationStepResult.Failed("El plan de energía guardado no es válido, así que no pude restaurarlo.");

    /// <summary>
    /// Pide el cambio y comprueba releyendo. Los dos fallos posibles se distinguen a propósito: que
    /// Windows rechace la llamada y que la acepte pero el plan activo siga siendo otro no son el
    /// mismo problema, y quien lo lea necesita saber cuál de los dos ocurrió.
    /// </summary>
    private OptimizationStepResult SetAndVerify(Guid scheme, string rejectedMessage)
    {
        if (!TrySetScheme(scheme))
        {
            return OptimizationStepResult.Failed(rejectedMessage);
        }

        var active = ReadCurrentPowerPlanId();
        if (active is null)
        {
            return OptimizationStepResult.Failed(
                "Cambié el plan de energía pero no pude releerlo para confirmarlo, así que no lo doy por hecho.");
        }

        return Guid.TryParse(active, out var current) && current == scheme
            ? OptimizationStepResult.Ok("Plan de energía verificado.")
            : OptimizationStepResult.Failed(
                "Windows aceptó el cambio pero el plan activo sigue siendo otro. " +
                "Puede que una directiva del sistema lo esté reponiendo.");
    }

    private static Guid? ResolveScheme(string changeId) => changeId switch
    {
        "power.highPerformance" => HighPerformance,
        "power.balanced" => Balanced,
        "power.saver" => PowerSaver,
        _ => null
    };

    private static bool TrySetScheme(Guid scheme)
    {
        try
        {
            return PowerSetActiveScheme(IntPtr.Zero, ref scheme) == 0;
        }
        catch (Exception exception) when (
            exception is EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
