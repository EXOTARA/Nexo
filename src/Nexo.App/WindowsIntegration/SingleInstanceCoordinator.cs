using System.Threading;

namespace Nexo.App.WindowsIntegration;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\Kohana.Desktop.SingleInstance";
    private const string ActivationEventName = @"Local\Kohana.Desktop.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly CancellationTokenSource _cancellation = new();
    private bool _ownsMutex;
    private Task? _listenerTask;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName);
        _activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName);

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

    public void Dispose()
    {
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

        _activationEvent.Dispose();
        _mutex.Dispose();
        _cancellation.Dispose();
    }
}
