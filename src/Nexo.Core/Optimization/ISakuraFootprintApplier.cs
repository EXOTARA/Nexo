using Nexo.Core.AdaptiveEngine;

namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D11 (Fase 4) — el segundo objetivo que Sakura aplica de verdad: su PROPIO consumo.
///
/// Por qué éste y no otro truco del sistema: la fase pide que los siete escenarios apliquen cambios
/// reversibles, y la regla que gobierna D8 sigue en pie — solo se aplica lo que se puede deshacer
/// con certeza. El modo de rendimiento de Sakura cumple las dos cosas a la vez: es un cambio real
/// que se nota en el equipo (los motores locales de voz e IA son lo más caro que corre aquí) y vive
/// en el archivo de preferencias de Sakura, así que revertirlo es escribir el valor anterior. Cero
/// riesgo de dejar Windows en un estado raro.
///
/// Es además el cambio más honesto que Sakura puede ofrecer: antes de pedirle al usuario que cierre
/// sus programas, la aplicación que se aparta primero es ella.
/// </summary>
public interface ISakuraFootprintApplier
{
    HardwarePerformanceMode ReadCurrentMode();

    OptimizationStepResult ApplyMode(HardwarePerformanceMode mode);
}
