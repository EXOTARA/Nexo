using Nexo.Core.Hardware;

namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D8 (Fase 4) — arma el plan a partir del hardware REAL del equipo
/// (<see cref="HardwareCapabilityProfile"/>), no de una lista fija de ajustes.
///
/// Regla del roadmap que gobierna toda esta clase: *"no es una lista genérica de tweaks de
/// internet — cada cambio debe justificarse con una medición real del equipo"*. En consecuencia,
/// **cuando falta el dato que justificaría un cambio, el cambio NO se propone**: se registra en
/// <see cref="OptimizationPlan.SkippedForMissingData"/> y se dice por qué. Proponer a ciegas sería
/// exactamente el comportamiento que la fase prohíbe.
///
/// Función pura: no mide, no aplica y no toca el sistema. Solo decide qué tendría sentido.
/// </summary>
public static class OptimizationPlanBuilder
{
    private const ulong GibiByte = 1024UL * 1024UL * 1024UL;

    public static OptimizationPlan Build(OptimizationScenario scenario, HardwareCapabilityProfile? profile)
    {
        if (scenario == OptimizationScenario.Restaurar)
        {
            return new OptimizationPlan(
                scenario,
                "Devolver el equipo al estado guardado antes del último plan aplicado.",
                [],
                []);
        }

        var snapshot = profile?.Snapshot;
        var changes = new List<OptimizationChange>();
        var skipped = new List<string>();

        AddPowerPlanChange(scenario, snapshot, changes, skipped);
        AddMemoryAdvice(scenario, snapshot, changes, skipped);
        AddGraphicsAdvice(scenario, snapshot, changes, skipped);

        return new OptimizationPlan(scenario, BuildSummary(scenario, changes.Count), changes, skipped);
    }

    private static void AddPowerPlanChange(
        OptimizationScenario scenario,
        HardwareCapabilitySnapshot? snapshot,
        List<OptimizationChange> changes,
        List<string> skipped)
    {
        var hasBattery = snapshot?.HasBattery;
        if (hasBattery is null)
        {
            skipped.Add(
                "Plan de energía: no se pudo determinar si este equipo tiene batería, así que no se " +
                "propone cambiarlo.");
            return;
        }

        switch (scenario)
        {
            case OptimizationScenario.Bateria when hasBattery == true:
                changes.Add(new OptimizationChange(
                    "power.saver",
                    "Cambiar a plan de energía «Economizador»",
                    "Este equipo tiene batería, así que reducir el consumo alarga la autonomía.",
                    OptimizationTarget.PowerPlan,
                    IsReversibleByKohana: true));
                break;

            case OptimizationScenario.Bateria:
                skipped.Add(
                    "Plan de energía: este equipo no tiene batería, así que un plan de ahorro no " +
                    "aportaría autonomía.");
                break;

            case OptimizationScenario.Jugar or OptimizationScenario.EdicionVideo:
                changes.Add(new OptimizationChange(
                    "power.highPerformance",
                    "Cambiar a plan de energía «Alto rendimiento»",
                    hasBattery == true
                        ? "Sostiene la frecuencia del procesador durante cargas largas. Al ser un " +
                          "equipo con batería, gastará más mientras esté activo."
                        : "Equipo sin batería: sostener la frecuencia del procesador no tiene coste " +
                          "de autonomía.",
                    OptimizationTarget.PowerPlan,
                    IsReversibleByKohana: true));
                break;

            case OptimizationScenario.General:
                changes.Add(new OptimizationChange(
                    "power.balanced",
                    "Cambiar a plan de energía «Equilibrado»",
                    "Equilibrado es el punto neutro de Windows y el que conviene para uso mixto.",
                    OptimizationTarget.PowerPlan,
                    IsReversibleByKohana: true));
                break;
        }
    }

    private static void AddMemoryAdvice(
        OptimizationScenario scenario,
        HardwareCapabilitySnapshot? snapshot,
        List<OptimizationChange> changes,
        List<string> skipped)
    {
        var totalBytes = snapshot?.Memory.TotalPhysicalBytes;
        if (totalBytes is null)
        {
            skipped.Add("Memoria: no se pudo medir la RAM instalada, así que no se propone nada sobre ella.");
            return;
        }

        var gigabytes = totalBytes.Value / (double)GibiByte;

        // El umbral depende del escenario: 8 GB dan de sobra para una videollamada y se quedan
        // cortos para editar video. Un único umbral fijo sería justo el "tweak genérico" que la
        // fase prohíbe.
        var demanding = scenario is OptimizationScenario.EdicionVideo or OptimizationScenario.Jugar;
        var threshold = demanding ? 16 : 8;

        if (gigabytes < threshold)
        {
            changes.Add(new OptimizationChange(
                "memory.closeApps",
                "Cerrar aplicaciones en segundo plano antes de empezar",
                $"Este equipo tiene {gigabytes:0.#} GB de RAM y {ScenarioLabel(scenario)} suele " +
                $"necesitar al menos {threshold} GB cómodos.",
                OptimizationTarget.Advice,
                IsReversibleByKohana: false));
        }
    }

    private static void AddGraphicsAdvice(
        OptimizationScenario scenario,
        HardwareCapabilitySnapshot? snapshot,
        List<OptimizationChange> changes,
        List<string> skipped)
    {
        if (scenario is not (OptimizationScenario.Jugar or OptimizationScenario.EdicionVideo))
        {
            return;
        }

        var adapter = snapshot?.PreferredGraphicsAdapter;
        if (adapter?.IsDedicated is null)
        {
            skipped.Add(
                "Gráficos: no se pudo determinar si hay una tarjeta dedicada, así que no se propone " +
                "nada sobre ella.");
            return;
        }

        if (adapter.IsDedicated == false)
        {
            changes.Add(new OptimizationChange(
                "graphics.integrated",
                "Bajar la calidad gráfica en la aplicación",
                $"El adaptador preferido de este equipo ({adapter.Name ?? "gráficos integrados"}) es " +
                "integrado, no dedicado: comparte memoria con el sistema.",
                OptimizationTarget.Advice,
                IsReversibleByKohana: false));
        }
    }

    private static string ScenarioLabel(OptimizationScenario scenario) => scenario switch
    {
        OptimizationScenario.Jugar => "jugar",
        OptimizationScenario.Programar => "programar",
        OptimizationScenario.EdicionVideo => "editar video",
        OptimizationScenario.Videollamada => "una videollamada",
        OptimizationScenario.Bateria => "trabajar con batería",
        OptimizationScenario.General => "el uso general",
        _ => scenario.ToString()
    };

    private static string BuildSummary(OptimizationScenario scenario, int changeCount) => changeCount == 0
        ? $"No encontré nada que valga la pena cambiar para {ScenarioLabel(scenario)} en este equipo."
        : $"Plan para {ScenarioLabel(scenario)}: {changeCount} " +
          (changeCount == 1 ? "cambio propuesto." : "cambios propuestos.");
}
