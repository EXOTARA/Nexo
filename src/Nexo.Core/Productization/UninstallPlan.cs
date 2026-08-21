namespace Nexo.Core.Productization;

/// <summary>Diseño D20 — qué hacer con tus datos al desinstalar. La persona elige; no hay defecto silencioso.</summary>
public enum UninstallDataChoice
{
    /// <summary>Se quedan donde están. Reinstalar los recupera.</summary>
    Conservar,

    /// <summary>Se borran. Sin vuelta atrás.</summary>
    Borrar
}

public sealed record UninstallPlan(
    IReadOnlyList<SakuraDataItem> PersonalData,
    IReadOnlyList<SakuraDataItem> OperationalData,
    UninstallDataChoice Choice)
{
    public IReadOnlyList<SakuraDataItem> WillBeDeleted =>
        Choice == UninstallDataChoice.Borrar ? [.. PersonalData, .. OperationalData] : OperationalData;

    public IReadOnlyList<SakuraDataItem> WillBeKept =>
        Choice == UninstallDataChoice.Borrar ? [] : PersonalData;
}

/// <summary>
/// Diseño D20 (Fase 9) — separa "la aplicación" de "tus datos" antes de desinstalar.
///
/// El criterio de terminado de la fase pide desinstalación limpia, y limpia no significa borrarlo
/// todo: significa que **nadie se lleva una sorpresa**. Desinstalar un programa y descubrir después
/// que se llevó años de notas es la sorpresa más cara que puede dar un instalador, y es la que se
/// evita aquí. Al revés también cuenta: dejar datos personales sin decirlo es dejar un rastro que la
/// persona creía eliminado.
///
/// Por eso el plan enumera **las dos listas** y no propone ninguna por omisión: los datos
/// operativos (registros, copias previas, marcas de pack) se van siempre porque Sakura los rehace, y
/// los personales solo se van si la persona lo dice.
/// </summary>
public static class UninstallPlanner
{
    public static UninstallPlan Build(UninstallDataChoice choice) => new(
        SakuraDataInventory.Personal,
        [.. SakuraDataInventory.All.Where(item => !item.IsPersonal)],
        Enum.IsDefined(choice) ? choice : UninstallDataChoice.Conservar);

    /// <summary>
    /// Resumen para enseñar antes de nada. Nombra lo que se borra Y lo que se queda: un resumen que
    /// solo cuenta una de las dos mitades es exactamente el que produce la sorpresa.
    /// </summary>
    public static string Describe(UninstallPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var builder = new System.Text.StringBuilder();

        if (plan.Choice == UninstallDataChoice.Borrar)
        {
            builder.AppendLine("Se borrará TODO, incluido lo tuyo. No se puede deshacer:");
            foreach (var item in plan.WillBeDeleted)
            {
                builder.Append("· ").Append(item.Title).Append(" — ").AppendLine(item.WhatItHolds);
            }

            return builder.ToString().TrimEnd();
        }

        builder.AppendLine("Se conservará lo tuyo, por si vuelves a instalar Sakura:");
        foreach (var item in plan.WillBeKept)
        {
            builder.Append("· ").Append(item.Title).Append(" — ").AppendLine(item.WhatItHolds);
        }

        builder.AppendLine();
        builder.AppendLine("Se borrarán los datos de funcionamiento, que Sakura rehace sola:");
        foreach (var item in plan.WillBeDeleted)
        {
            builder.Append("· ").AppendLine(item.Title);
        }

        return builder.ToString().TrimEnd();
    }
}
