using Nexo.Windows.WindowsIntegration;

namespace Nexo.Windows.Tests.Characterization;

/// <summary>
/// Fase 1.1 — congela la conducta de segunda instancia (escenario 6 de `TEST_MATRIX.md`:
/// *"enfoca la existente; no duplica"*) tal como la implementa hoy
/// <see cref="SingleInstanceCoordinator"/> y la consume <c>App.OnStartup</c>.
///
/// Cada prueba usa una clave de instancia única para no interferir con una instancia real de
/// Kohana que el usuario pueda tener abierta.
///
/// <para>
/// <b>Detalle importante descubierto en 1.1:</b> el guardián se apoya en un
/// <see cref="Mutex"/> con nombre, y la propiedad de un mutex de Win32 es **por hilo**, no por
/// proceso. Crear dos coordinadores en el mismo hilo hace que *ambos* se consideren primarios,
/// porque el segundo `WaitOne` es una adquisición recursiva del mismo dueño. Por eso la segunda
/// instancia se simula siempre en un hilo dedicado, que es lo que ocurre en la realidad
/// (procesos distintos).
/// </para>
/// </summary>
public sealed class SingleInstanceCharacterizationTests
{
    private static string NewKey() => "test-" + Guid.NewGuid().ToString("n");

    /// <summary>
    /// Ejecuta <paramref name="body"/> en un hilo propio y espera el resultado, para que la
    /// propiedad del mutex no se herede del hilo de la prueba.
    /// </summary>
    private static T OnDedicatedThread<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "El hilo de prueba no terminó.");

        if (failure is not null)
        {
            throw failure;
        }

        return result;
    }

    [Fact]
    public void TheFirstInstance_OwnsThePrimaryRole()
    {
        using var first = new SingleInstanceCoordinator(NewKey());

        Assert.True(first.IsPrimaryInstance);
    }

    [Fact]
    public void ASecondInstanceInAnotherThread_IsNotPrimary()
    {
        var key = NewKey();
        using var first = new SingleInstanceCoordinator(key);
        Assert.True(first.IsPrimaryInstance);

        var secondIsPrimary = OnDedicatedThread(() =>
        {
            using var second = new SingleInstanceCoordinator(key);
            return second.IsPrimaryInstance;
        });

        Assert.False(secondIsPrimary);
    }

    [Fact]
    public void MutexOwnershipIsPerThread_NotPerProcess()
    {
        // Congela la razón por la que la prueba anterior necesita un hilo: en el mismo hilo,
        // el segundo coordinador también se cree primario. En producción no ocurre porque
        // cada instancia es un proceso distinto, pero es una trampa real para quien extraiga
        // este componente en la fase 1.2.
        var key = NewKey();
        using var first = new SingleInstanceCoordinator(key);
        using var sameThread = new SingleInstanceCoordinator(key);

        Assert.True(first.IsPrimaryInstance);
        Assert.True(sameThread.IsPrimaryInstance);
    }

    [Fact]
    public void DifferentInstanceKeys_DoNotCollide()
    {
        using var a = new SingleInstanceCoordinator(NewKey());

        var bIsPrimary = OnDedicatedThread(() =>
        {
            using var b = new SingleInstanceCoordinator(NewKey());
            return b.IsPrimaryInstance;
        });

        Assert.True(a.IsPrimaryInstance);
        Assert.True(bIsPrimary);
    }

    [Fact]
    public void ASecondInstance_ActivatesTheExistingWindowInsteadOfOpeningAnother()
    {
        var key = NewKey();
        using var primary = new SingleInstanceCoordinator(key);
        using var activated = new ManualResetEventSlim(false);

        primary.ActivationRequested += (_, _) => activated.Set();
        primary.StartListening();

        var secondWasPrimary = OnDedicatedThread(() =>
        {
            using var second = new SingleInstanceCoordinator(key);
            var isPrimary = second.IsPrimaryInstance;
            second.SignalPrimaryInstance();
            return isPrimary;
        });

        Assert.False(secondWasPrimary);
        Assert.True(
            activated.Wait(TimeSpan.FromSeconds(5)),
            "La instancia primaria no recibió la petición de activación.");
    }

    [Fact]
    public void ReleasingThePrimary_LetsTheNextInstanceTakeOver()
    {
        var key = NewKey();

        var first = new SingleInstanceCoordinator(key);
        Assert.True(first.IsPrimaryInstance);
        first.Dispose();

        var secondIsPrimary = OnDedicatedThread(() =>
        {
            using var second = new SingleInstanceCoordinator(key);
            return second.IsPrimaryInstance;
        });

        Assert.True(secondIsPrimary);
    }

    [Fact]
    public void ANonPrimaryInstance_NeverStartsListening()
    {
        // Solo la primaria escucha activaciones; la secundaria únicamente señala y se cierra.
        var key = NewKey();
        using var primary = new SingleInstanceCoordinator(key);

        var raised = OnDedicatedThread(() =>
        {
            using var second = new SingleInstanceCoordinator(key);
            Assert.False(second.IsPrimaryInstance);

            var observed = false;
            second.ActivationRequested += (_, _) => observed = true;
            second.StartListening();
            second.SignalPrimaryInstance();
            Thread.Sleep(200);
            return observed;
        });

        Assert.False(raised);
    }

    [Fact]
    public void Dispose_IsNotIdempotent_KnownGap()
    {
        // HALLAZGO DE LA FASE 1.1: un segundo `Dispose` lanza `ObjectDisposedException`
        // porque `_cancellation.Cancel()` se invoca sobre un `CancellationTokenSource` ya
        // liberado. Hoy no se manifiesta porque `App.OnExit` pone el campo a null tras
        // liberar, pero la extracción de la fase 1.2 (contenedor de DI, que sí llama a
        // Dispose de forma genérica) puede sacarlo a la luz.
        var coordinator = new SingleInstanceCoordinator(NewKey());
        coordinator.Dispose();

        Assert.Throws<ObjectDisposedException>(coordinator.Dispose);
    }

    [Fact]
    public void SignallingWithNoPrimaryListening_DoesNotThrow()
    {
        var key = NewKey();
        using var lone = new SingleInstanceCoordinator(key);

        var exception = Record.Exception(lone.SignalPrimaryInstance);

        Assert.Null(exception);
    }
}
