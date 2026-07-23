namespace Nexo.Core.Automation;

/// <summary>
/// Con cuánta certeza el texto del usuario pidió una **rutina** en concreto.
///
/// Existe porque el verbo de ejecución es compartido: "inicia" sirve igual para
/// "inicia la rutina estudio" que para "inicia un temporizador de 20 minutos". Sin esta
/// distinción, el parser de rutinas se queda con cualquier frase que empiece por
/// "ejecuta/inicia/activa/corre" y eclipsa a los parsers de enfoque y tareas (defecto D1 de la
/// fase 1.1).
/// </summary>
public enum RoutineMatchConfidence
{
    /// <summary>
    /// El texto nombra explícitamente el dominio de rutinas ("la <b>rutina</b> estudio",
    /// "abre mis rutinas", "modo programación"). La intención no es ambigua y tiene
    /// prioridad sobre el resto de dominios.
    /// </summary>
    Explicit,

    /// <summary>
    /// El texto solo comparte el verbo ("inicia X") sin nombrar el dominio. Es una
    /// **hipótesis**: solo debe ejecutarse si ningún dominio más específico reclama la frase
    /// y además existe realmente una rutina con ese nombre.
    /// </summary>
    Inferred
}
