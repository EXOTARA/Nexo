using System.Threading;

namespace Nexo.Windows.WindowsIntegration;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\Kohana.Desktop.SingleInstance";
    private const string ActivationEventName = @"Local\Kohana.Desktop.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly CancellationTokenSource _cancellation = new();
    private bool _ownsMutex;
    private bool _disposed;
    private Task? _listenerTask;

    /// <param name="instanceKey">
    /// Sufijo para aislar los objetos de sincronización. La aplicación siempre usa el valor por
    /// defecto (<c>null</c>), que conserva exactamente los nombres históricos; las pruebas de
    /// caracterización pasan una clave única para no colisionar con una instancia real de
    /// Kohana en ejecución. Introducido en la fase 1.1 como seam de prueba, sin cambiar la
    /// conducta de producción.
    /// </param>
    public SingleInstanceCoordinator(string? instanceKey = null)
    {
        var suffix = string.IsNullOrWhiteSpace(instanceKey) ? string.Empty : "." + instanceKey;

        _mutex = new Mutex(initiallyOwned: false, MutexName + suffix);
        _activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName + suffix);

        try
        {
            _ownsMutex = _mutex.WaitOne(TimeSpan.Zero, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }
    }

    public bool IsPrimaryInstance => _ownsMutex;

    public event EventHandler? ActivationRequested;

    public void SignalPrimaryInstance()
    {
        try
        {
            _activationEvent.Set();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void StartListening()
    {
        if (!_ownsMutex || _listenerTask is not null)
        {
            return;
        }

        _listenerTask = Task.Run(() =>
        {
            var handles = new WaitHandle[]
            {
                _activationEvent,
                _cancellation.Token.WaitHandle
            };

            while (!_cancellation.IsCancellationRequested)
            {
                var signaled = WaitHandle.WaitAny(handles);
                if (signaled == 1 || _cancellation.IsCancellationRequested)
                {
                    break;
                }

                ActivationRequested?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    /// <summary>
    /// Libera el mutex, el evento de activación y los suscriptores. Es **idempotente**:
    /// llamarlo dos veces no lanza. Antes, la segunda llamada lanzaba
    /// <see cref="ObjectDisposedException"/> al cancelar un <see cref="CancellationTokenSource"/>
    /// ya liberado (defecto D4 de la fase 1.1). No se manifestaba porque <c>App.OnExit</c>
    /// pone el campo a <c>null</c>, pero un contenedor de DI libera de forma genérica.
    ///
    /// La guarda **previene** la excepción en lugar de capturarla, para no enmascarar errores
    /// reales de uso tras liberación en el resto de los miembros.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();

        try
        {
            _activationEvent.Set();
        }
        catch (ObjectDisposedException)
        {
        }

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _ownsMutex = false;
        }

        // El hilo de escucha puede seguir despertando: sin esto, un suscriptor podría
        // recibir una activación de una instancia ya liberada.
        ActivationRequested = null;

        _activationEvent.Dispose();
        _mutex.Dispose();
        _cancellation.Dispose();
    }
}
