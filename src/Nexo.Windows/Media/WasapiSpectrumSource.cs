using NAudio.Dsp;
using NAudio.Wave;
using Nexo.Core.Media;

namespace Nexo.Windows.Media;

/// <summary>
/// Diseño D46 — escucha lo que Windows está sacando por los altavoces y lo entrega como bandas.
///
/// Es captura en bucle (<c>WasapiLoopbackCapture</c>): lee la mezcla que va al dispositivo de
/// salida, no un micrófono. **No graba nada y no toca la entrada de audio** — la distinción importa
/// en una aplicación que pide permiso para el micrófono por otras razones, y por eso el nombre de
/// esta clase dice de dónde sale el sonido.
///
/// La ventana de la FFT es de 2048 muestras. Menos deja las notas graves sin resolución —a 48 kHz,
/// 1024 muestras dan bins de 47 Hz y ahí abajo eso es media octava por bin— y más añade un retraso
/// que se ve: el anillo iría por detrás de la música.
/// </summary>
public sealed class WasapiSpectrumSource : IDisposable
{
    private const int FftLength = 2048;
    private const int FftOrder = 11; // 2^11 = 2048

    private readonly object _sync = new();
    private readonly float[] _window = new float[FftLength];
    private readonly float[] _pending = new float[FftLength];
    private readonly double[] _magnitudes = new double[FftLength / 2];

    private WasapiLoopbackCapture? _capture;
    private int _pendingCount;
    private int _sampleRate;
    private bool _disposed;
    private bool _unavailable;

    public WasapiSpectrumSource()
    {
        // Ventana de Hann. Sin ventana, cortar el audio en trozos introduce bordes bruscos que la
        // FFT interpreta como agudos que no existen: el anillo se llenaría de barras altas
        // fantasma en la zona derecha con cualquier sonido.
        for (var i = 0; i < FftLength; i++)
        {
            _window[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftLength - 1))));
        }
    }

    /// <summary>Muestras por segundo de la captura activa, o 0 si no hay ninguna.</summary>
    public int SampleRate
    {
        get { lock (_sync) { return _sampleRate; } }
    }

    public bool IsRunning
    {
        get { lock (_sync) { return _capture is not null; } }
    }

    /// <summary>
    /// Arranca la captura si no estaba. Devuelve falso cuando este equipo no la permite —una
    /// sesión remota, un dispositivo exclusivo— y en ese caso no se reintenta: el visualizador se
    /// queda quieto, que es mejor que insistir treinta veces por segundo contra algo que no va a
    /// funcionar.
    /// </summary>
    public bool Start()
    {
        lock (_sync)
        {
            if (_disposed || _unavailable)
            {
                return false;
            }

            if (_capture is not null)
            {
                return true;
            }

            try
            {
                var capture = new WasapiLoopbackCapture();
                _sampleRate = capture.WaveFormat.SampleRate;
                capture.DataAvailable += OnDataAvailable;
                capture.RecordingStopped += OnRecordingStopped;
                capture.StartRecording();
                _capture = capture;
                return true;
            }
            catch (Exception)
            {
                _unavailable = true;
                _capture = null;
                return false;
            }
        }
    }

    public void Stop()
    {
        WasapiLoopbackCapture? capture;

        lock (_sync)
        {
            capture = _capture;
            _capture = null;
            _pendingCount = 0;
        }

        if (capture is null)
        {
            return;
        }

        try
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.StopRecording();
            capture.Dispose();
        }
        catch (Exception)
        {
            // Detener algo que ya se cayó solo no es un problema que nadie deba ver.
        }
    }

    /// <summary>
    /// Vuelca las magnitudes del último bloque completo en <paramref name="spectrum"/>.
    ///
    /// Lo consume quien dibuja, a su propio ritmo, en vez de empujarle un evento por cada bloque de
    /// audio: la tarjeta entrega bloques cada pocos milisegundos y la pantalla solo puede enseñar
    /// sesenta fotogramas por segundo, así que empujar significaría descartar la mayoría después de
    /// haber pagado el salto de hilo.
    /// </summary>
    public bool Fill(AudioSpectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        double[] magnitudes;
        int sampleRate;

        lock (_sync)
        {
            if (_capture is null || _pendingCount < FftLength)
            {
                return false;
            }

            var buffer = new Complex[FftLength];
            for (var i = 0; i < FftLength; i++)
            {
                buffer[i].X = _pending[i] * _window[i];
                buffer[i].Y = 0;
            }

            FastFourierTransform.FFT(true, FftOrder, buffer);

            for (var i = 0; i < _magnitudes.Length; i++)
            {
                _magnitudes[i] = Math.Sqrt((buffer[i].X * buffer[i].X) + (buffer[i].Y * buffer[i].Y));
            }

            _pendingCount = 0;
            magnitudes = _magnitudes;
            sampleRate = _sampleRate;
        }

        spectrum.Push(magnitudes, sampleRate);
        return true;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        var format = _capture?.WaveFormat;
        if (format is null || format.BitsPerSample != 32 || format.Channels <= 0)
        {
            return;
        }

        var channels = format.Channels;
        var frames = e.BytesRecorded / 4 / channels;

        lock (_sync)
        {
            for (var frame = 0; frame < frames && _pendingCount < FftLength; frame++)
            {
                // Los canales se promedian a mono. Un anillo por canal sería dos anillos que dicen
                // casi lo mismo y se estorban.
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    sum += BitConverter.ToSingle(e.Buffer, ((frame * channels) + channel) * 4);
                }

                _pending[_pendingCount++] = sum / channels;
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        lock (_sync)
        {
            // Cambiar de dispositivo de salida detiene la captura. Se suelta la referencia para que
            // el siguiente Start la reabra sobre el dispositivo nuevo.
            _capture = null;
            _pendingCount = 0;
        }
    }

    public void Dispose()
    {
        Stop();

        lock (_sync)
        {
            _disposed = true;
        }
    }
}
