using System.Runtime.InteropServices;
using Nexo.Core.Optimization;

namespace Nexo.Windows.Optimization;

/// <summary>
/// Diseño D8 (Fase 4) — aplica los cambios del plan usando **exclusivamente API documentada de
/// Win32** (<c>PowerGetActiveScheme</c> / <c>PowerSetActiveScheme</c>, de powrprof.dll). Es una
/// decisión explícita registrada en el roadmap: nada de trucos de registro ni de lanzar procesos,
/// para no disparar falsos positivos de antivirus.
///
/// Alcance honesto de esta primera versión: el único cambio que Kohana APLICA es el plan de
/// energía, porque es el que puede revertirse por completo y con certeza (se guarda el GUID
/// anterior y se vuelve a él). Todo lo demás del plan son consejos que ejecuta la persona. Es
/// preferible aplicar poco y poder deshacerlo siempre, que aplicar mucho sin poder garantizar la
/// vuelta atrás — el roadmap marca la reversión como requisito, no como aspiración.
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

    public OptimizationApplyResult Apply(OptimizationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var applied = 0;
        foreach (var change in plan.ApplicableChanges)
        {
            if (change.Target != OptimizationTarget.PowerPlan)
            {
                continue;
            }

            var scheme = ResolveScheme(change.Id);
            if (scheme is null)
            {
                return OptimizationApplyResult.Failed(
                    $"No reconozco el cambio «{change.Id}», así que no apliqué nada.");
            }

            if (!TrySetScheme(scheme.Value))
            {
                return OptimizationApplyResult.Failed(
                    "Windows no aceptó el cambio de plan de energía.");
            }

            applied++;
        }

        return applied == 0
            ? OptimizationApplyResult.Failed("Este plan no tiene cambios que Kohana pueda aplicar.")
            : OptimizationApplyResult.Applied(applied, "Listo. Puedes deshacerlo cuando quieras.");
    }

    public OptimizationApplyResult Restore(OptimizationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!Guid.TryParse(snapshot.PreviousPowerPlanId, out var previous))
        {
            return OptimizationApplyResult.Failed(
                "No hay un estado anterior guardado que restaurar.");
        }

        return TrySetScheme(previous)
            ? OptimizationApplyResult.Applied(1, "Devolví el plan de energía a como estaba.")
            : OptimizationApplyResult.Failed(
                "Windows no aceptó restaurar el plan de energía anterior.");
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
