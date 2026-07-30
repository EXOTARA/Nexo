using System.Runtime.InteropServices;
using Nexo.Core.Ambient;
using Nexo.Core.Flow;
using Nexo.Windows.Flow;

namespace Nexo.Windows.Tests.Flow;

/// <summary>
/// Diseño D6.2 (Fase 3 — Kohana Flow).
///
/// **Aquí solo se prueban los casos en los que el insertor SE NIEGA a escribir, y es a propósito.**
/// Una prueba del camino feliz tendría que llamar a <c>SendInput</c> de verdad, y <c>SendInput</c>
/// escribe en la ventana que tenga el foco en ese momento — es decir, en cualquier ventana que la
/// persona (o el agente de CI) tuviera abierta mientras corre la suite. Una prueba que teclea texto
/// dentro de una ventana ajena no es una prueba, es un efecto secundario. La inserción real se
/// valida a mano, no aquí.
///
/// Las negativas, en cambio, son justo la parte crítica de seguridad de esta clase, y sí se pueden
/// comprobar de forma determinista sin escribir una sola tecla.
/// </summary>
public sealed class WindowsFlowTextInserterTests
{
    [Fact]
    public void Insert_WithEmptyText_RefusesWithoutTyping()
    {
        var inserter = new WindowsFlowTextInserter(new FakeContextProvider());

        var result = inserter.Insert(string.Empty, expectedWindowHandle: 12345);

        Assert.False(result.IsInserted);
        Assert.Equal(FlowInsertionFailure.EmptyText, result.Failure);
    }

    [Fact]
    public void Insert_WithNoRememberedWindow_RefusesWithoutTyping()
    {
        var inserter = new WindowsFlowTextInserter(new FakeContextProvider());

        var result = inserter.Insert("hola", expectedWindowHandle: 0);

        Assert.False(result.IsInserted);
        Assert.Equal(FlowInsertionFailure.NoTargetWindow, result.Failure);
    }

    /// <summary>
    /// La garantía central de D6.2: si el foco cambió, no se escribe. Se pasa un handle que con
    /// certeza no es la ventana en primer plano.
    /// </summary>
    [Fact]
    public void Insert_WhenFocusMovedToAnotherWindow_RefusesWithoutTyping()
    {
        var inserter = new WindowsFlowTextInserter(new FakeContextProvider());

        var result = inserter.Insert("texto privado", expectedWindowHandle: 0x7FFFFFFF);

        Assert.False(result.IsInserted);
        Assert.Equal(FlowInsertionFailure.FocusChanged, result.Failure);
    }

    [Fact]
    public void Insert_IntoASensitiveWindow_RefusesWithoutTyping()
    {
        // Se usa la ventana real en primer plano para que el guardia de foco pase y se llegue a
        // comprobar la sensibilidad; el proveedor falso la declara sensible, así que nunca se
        // llega a teclear nada.
        var foreground = GetForegroundWindow().ToInt64();
        if (foreground == 0)
        {
            return; // Sesión sin ventana en primer plano: no hay nada que comprobar aquí.
        }

        var inserter = new WindowsFlowTextInserter(
            new FakeContextProvider(new AmbientContextSnapshot(null, null, IsSensitive: true)));

        var result = inserter.Insert("mi contraseña", foreground);

        Assert.False(result.IsInserted);
        Assert.Equal(FlowInsertionFailure.SensitiveWindow, result.Failure);
    }

    [Fact]
    public void Insert_WhenTheTargetWindowVanished_RefusesWithoutTyping()
    {
        var foreground = GetForegroundWindow().ToInt64();
        if (foreground == 0)
        {
            return;
        }

        var inserter = new WindowsFlowTextInserter(new FakeContextProvider(snapshot: null));

        var result = inserter.Insert("hola", foreground);

        Assert.False(result.IsInserted);
        Assert.Equal(FlowInsertionFailure.NoTargetWindow, result.Failure);
    }

    private sealed class FakeContextProvider(AmbientContextSnapshot? snapshot = null)
        : IAmbientContextProvider
    {
        public AmbientContextSnapshot? Capture(long windowHandle) => snapshot;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
