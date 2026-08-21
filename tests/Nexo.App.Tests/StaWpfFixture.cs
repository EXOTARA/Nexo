using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Nexo.App.Tests;

/// <summary>
/// Un único hilo STA con un <see cref="Application"/> y las hojas de estilo reales de Sakura
/// cargadas una sola vez, compartido por TODAS las clases de prueba que lo usen. WPF exige que
/// <see cref="Application"/> sea un singleton por proceso, así que crear una instancia nueva por
/// prueba lanzaría "Application already exists" en la segunda prueba — de ahí el fixture
/// compartido en vez de un hilo por método de prueba.
///
/// Se expone mediante <see cref="StaWpfCollection"/> (fixture de colección) y no
/// <c>IClassFixture</c>: con IClassFixture xUnit crea una instancia POR CLASE, de modo que la
/// segunda clase de pruebas que lo usara volvería a construir un <see cref="Application"/> y
/// fallaría. La colección también serializa esas clases en un solo hilo, que es justo lo que
/// necesita un Dispatcher STA compartido.
/// </summary>
public sealed class StaWpfFixture : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(initialState: false);
    private readonly Lock _pendingDispatcherExceptionLock = new();

    public Dispatcher Dispatcher { get; private set; } = null!;

    private Exception? _startupException;
    private Exception? _pendingDispatcherException;

    public StaWpfFixture()
    {
        _thread = new Thread(() =>
        {
            try
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

                // Sin este manejador, una excepción sin observar que llega por una continuación
                // async void encolada en el Dispatcher (p. ej. el Loaded de AudioView, que sigue
                // ejecutando después de que Invoke() ya devolvió el control) es fatal para el
                // hilo: WPF puede intentar mostrar un diálogo de error de Windows que, en una
                // sesión sin escritorio interactivo, se queda esperando para siempre y cuelga
                // Dispatcher.Run() — y con él, todas las pruebas siguientes de este fixture
                // compartido. Al marcar e.Handled = true y guardar la excepción, el hilo STA
                // sigue vivo y Invoke() puede relanzarla de forma determinista.
                application.DispatcherUnhandledException += (_, e) =>
                {
                    e.Handled = true;
                    lock (_pendingDispatcherExceptionLock)
                    {
                        _pendingDispatcherException ??= e.Exception;
                    }
                };

                // El ensamblado de Nexo.App se compila como "Sakura" (AssemblyName en el csproj),
                // así que el URI de pack debe usar ese nombre, no el nombre del proyecto/namespace.
                var themeDictionary = new ResourceDictionary
                {
                    // Diseño D70 — el ensamblado se llama Sakura desde el cambio de nombre; este URI lo
                    // nombra directamente, así que se mueve con él.
                    Source = new Uri("/Sakura;component/Themes/ThemeResources.xaml", UriKind.Relative)
                };
                Application.Current.Resources.MergedDictionaries.Add(themeDictionary);

                Dispatcher = Dispatcher.CurrentDispatcher;
                _ready.Set();
                Dispatcher.Run();
            }
            catch (Exception exception)
            {
                _startupException = exception;
                _ready.Set();
            }
        })
        {
            IsBackground = true
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();

        if (_startupException is not null)
        {
            throw new InvalidOperationException(
                "No se pudo inicializar WPF en el hilo STA de pruebas.", _startupException);
        }
    }

    /// <summary>
    /// Ejecuta <paramref name="action"/> en el hilo STA compartido y relanza cualquier excepción
    /// que ocurra dentro, preservando el tipo y el stack trace original — así xunit reporta la
    /// excepción real (p. ej. XamlParseException) en vez de una genérica de Dispatcher.
    /// </summary>
    public void Invoke(Action action)
    {
        Exception? captured = null;
        Dispatcher.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
        });

        // Vacía la cola del Dispatcher hasta ApplicationIdle: una continuación async void
        // suspendida en un await (p. ej. tras Task.Run) se reanuda como una operación aparte,
        // encolada después de que la llamada anterior ya devolvió el control. Sin este drenaje,
        // esa excepción quedaría pendiente y se dispararía en medio de la SIGUIENTE llamada a
        // Invoke — u ocultarse por completo si esa próxima llamada nunca llega.
        Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

        var pendingDispatcherException = TakePendingDispatcherException();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }

        if (pendingDispatcherException is not null)
        {
            ExceptionDispatchInfo.Capture(pendingDispatcherException).Throw();
        }
    }

    private Exception? TakePendingDispatcherException()
    {
        lock (_pendingDispatcherExceptionLock)
        {
            var exception = _pendingDispatcherException;
            _pendingDispatcherException = null;
            return exception;
        }
    }

    public void Dispose()
    {
        Dispatcher.InvokeShutdown();
        _thread.Join(TimeSpan.FromSeconds(5));

        // Si una excepción del Dispatcher llegó después del último Invoke() de la prueba (p. ej.
        // durante el drenaje final o el propio apagado), no debe desaparecer en silencio: mejor
        // fallar aquí, de forma determinista, que dejar pasar una regresión real sin reportarla.
        var pendingDispatcherException = TakePendingDispatcherException();
        if (pendingDispatcherException is not null)
        {
            ExceptionDispatchInfo.Capture(pendingDispatcherException).Throw();
        }
    }
}

/// <summary>
/// Colección de pruebas que comparten el único <see cref="Application"/> WPF del proceso.
/// Toda clase de pruebas que necesite WPF real debe marcarse con
/// <c>[Collection(StaWpfCollection.Name)]</c> y recibir <see cref="StaWpfFixture"/> por
/// constructor.
/// </summary>
[CollectionDefinition(Name)]
public sealed class StaWpfCollection : ICollectionFixture<StaWpfFixture>
{
    public const string Name = "STA WPF";
}
