using Nexo.Core.Settings;

namespace Nexo.Core.Skills;

public enum SkillPackId
{
    Study,

    Dev
}

/// <summary>
/// Diseño D15 (Fase 8) — un ajuste que un pack cambia. Se lee y se escribe como TEXTO a propósito:
/// así el estado anterior cabe en un archivo JSON legible y deshacer un pack es volver a escribir lo
/// que había, sin que haga falta guardar código.
/// </summary>
public sealed record SkillPackSetting(
    string Id,
    string Title,
    Func<ShellPreferences, string> Read,
    Action<ShellPreferences, string> Write,
    string DesiredValue);

/// <summary>
/// Diseño D15 — algo que el pack NECESITA pero **no concede**. Es la regla que sostiene toda la
/// fase: *"cada pack hereda los permisos de las capacidades que combina — no introduce
/// excepciones"*. Un pack que encendiera la memoria o autorizara una carpeta por su cuenta sería una
/// forma de conceder permisos sin pedirlos, disfrazada de comodidad.
/// </summary>
public sealed record SkillPackRequirement(
    string Title,
    string WhyItMatters,
    Func<ShellPreferences, bool> IsSatisfied,
    string HowToEnable);

/// <summary>
/// Diseño D15 (Fase 8 — Skills Platform) — un pack combina capacidades YA implementadas y las deja
/// configuradas para un propósito. El criterio de terminado de la fase lo dice literal: *"al menos
/// dos packs completos usando exclusivamente capacidades ya implementadas en fases anteriores"*.
/// Ningún pack inventa capacidad nueva; si algo no existe, no entra en un pack.
/// </summary>
public sealed record SkillPack(
    SkillPackId Id,
    string Name,
    string Purpose,
    IReadOnlyList<SkillPackSetting> Settings,
    IReadOnlyList<SkillPackRequirement> Requirements);
