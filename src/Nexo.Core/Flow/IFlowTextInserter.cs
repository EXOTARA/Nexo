namespace Nexo.Core.Flow;

/// <summary>
/// Diseño D6.2 (Fase 3 — Sakura Flow) — escribe el texto ya dictado y normalizado en la aplicación
/// que el usuario tenía activa. Es la única parte de Sakura que envía entrada a otro programa, así
/// que el contrato es deliberadamente estrecho: se le pasa la ventana que se recordó AL EMPEZAR el
/// dictado, y la implementación debe negarse a escribir si esa ya no es la ventana en primer plano
/// (ver <see cref="FlowInsertionFailure.FocusChanged"/>).
/// </summary>
public interface IFlowTextInserter
{
    FlowInsertionResult Insert(string text, long expectedWindowHandle);
}
