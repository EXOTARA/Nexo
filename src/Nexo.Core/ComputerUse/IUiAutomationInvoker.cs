namespace Nexo.Core.ComputerUse;

/// <summary>
/// Diseño D19 (Fase 7, escalón 5) — invoca UN control de una ventana ajena.
///
/// Interfaz separada de <c>IUiAutomationReader</c> a propósito, igual que el proyecto separó lector
/// y escritor: quien solo lee no puede pulsar ni por accidente, y quien pide esto está pidiendo algo
/// distinto y visible en el código. El comentario de <c>UiAutomationElement</c> desde D5.3 decía que
/// nunca incluiría una acción para invocarlo — sigue siendo cierto: la acción no vive en el
/// elemento, vive aquí, detrás de su propia interfaz y su propio permiso.
///
/// **No decide nada.** Permisos, ambigüedad y ventana sensible son de
/// <see cref="UiAutomationInvokePolicy"/> y del coordinador. Aquí solo se pulsa.
/// </summary>
public interface IUiAutomationInvoker
{
    /// <summary>
    /// Invoca el control cuyo nombre coincide exactamente. Se le vuelve a pasar el nombre —y no un
    /// identificador del elemento leído antes— porque entre leer y pulsar la ventana pudo cambiar:
    /// se resuelve otra vez, contra el árbol de ahora.
    /// </summary>
    ComputerUseStepResult Invoke(long windowHandle, string controlName);
}
