using System.Net.Http;
using System.Text;
using NAudio;
using NAudio.Wave;
using Nexo.Core.Voice;
using Whisper.net;
using Whisper.net.Ggml;

namespace Nexo.Windows.Voice;

/// <summary>
/// Entrada de voz local basada en Whisper. El modelo se conserva en LocalAppData,
/// pero solo se carga en memoria mientras se transcribe una orden.
/// </summary>
public sealed class WhisperVoiceInputService : IVoiceInputService
{
    private const string CommandPrompt =
        "Órdenes breves para Nexo en español: abre PowerShell; muestra Peek; " +
        "cómo está mi PC; baja Spotify; sube Spotify al 50 por ciento; " +
        "silencia Discord; quita el silencio de Discord.";

    private const int SampleRate = 16_000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;
    private const long MinimumModelBytes = 50L * 1024 * 1024;
    private const long MinimumRecordedBytes = 4_800;
    private const long ProgressStepBytes = 4L * 1024 * 1024;
    private const int SpeechAverageAmplitudeThreshold = 420;

    private static readonly TimeSpan RecordingStopTimeout = TimeSpan.FromSeconds(2);

    private readonly object _sync = new();
    private readonly SemaphoreSlim _prepareGate = new(1, 1);
    private readonly string _modelPath;
    private readonly string _temporaryDirectory;

    private WaveInEvent? _recorder;
    private WaveFileWriter? _writer;
    private TaskCompletionSource<Exception?>? _recordingStopped;
    private string? _recordingPath;
    private long _recordedBytes;
    private TaskCompletionSource<bool>? _automaticUtteranceCompleted;
    private TimeSpan _automaticTrailingSilence;
    private int _automaticSilentMilliseconds;
    private bool _automaticSpeechDetected;
    private bool _automaticListening;
    private bool _disposed;

    public WhisperVoiceInputService()
    {
        var nexoDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexo");

        _modelPath = Path.Combine(nexoDirectory, "Models", "ggml-base.bin");
        _temporaryDirectory = Path.Combine(nexoDirectory, "Temp");
        IsReady = IsUsableModelFile();
    }

    public bool IsReady { get; private set; }

    public bool IsListening { get; private set; }

    public async Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsUsableModelFile())
        {
            IsReady = true;
            return VoicePreparationResult.Ready(
                "Voz local lista · Whisper base · español");
        }

        await _prepareGate.WaitAsync(cancellationToken);
        try
        {
            if (IsUsableModelFile())
            {
                IsReady = true;
                return VoicePreparationResult.Ready(
                    "Voz local lista · Whisper base · español");
            }

            progress?.Report(VoicePreparationProgress.Preparing(
                "Preparando la voz local por primera vez…"));

            var modelDirectory = Path.GetDirectoryName(_modelPath)
                ?? throw new InvalidOperationException("No se pudo resolver la carpeta del modelo.");
            Directory.CreateDirectory(modelDirectory);

            var partialPath = _modelPath + ".download";
            TryDeleteFile(partialPath);

            long totalBytes = 0;
            await using (var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(
                    GgmlType.Base,
                    cancellationToken: cancellationToken))
            await using (var modelWriter = new FileStream(
                partialPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                useAsync: true))
            {
                var buffer = new byte[81_920];
                long lastReportedBytes = 0;

                while (true)
                {
                    var bytesRead = await modelStream.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    await modelWriter.WriteAsync(
                        buffer.AsMemory(0, bytesRead),
                        cancellationToken);
                    totalBytes += bytesRead;

                    if (totalBytes - lastReportedBytes >= ProgressStepBytes)
                    {
                        lastReportedBytes = totalBytes;
                        progress?.Report(VoicePreparationProgress.Downloading(totalBytes));
                    }
                }

                await modelWriter.FlushAsync(cancellationToken);
            }

            if (totalBytes < MinimumModelBytes)
            {
                TryDeleteFile(partialPath);
                IsReady = false;
                return VoicePreparationResult.Unavailable(
                    "La descarga del modelo quedó incompleta. Revisa tu conexión e inténtalo de nuevo.");
            }

            File.Move(partialPath, _modelPath, overwrite: true);
            IsReady = true;

            progress?.Report(VoicePreparationProgress.Preparing(
                "Modelo descargado. La voz local está lista."));

            return VoicePreparationResult.Ready(
                "Voz local lista · Whisper base · español");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            IsReady = false;
            return VoicePreparationResult.Unavailable(
                "La descarga del modelo tardó demasiado. Inténtalo otra vez.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            IsReady = false;
            return VoicePreparationResult.Unavailable(
                "No pude preparar Whisper local. Revisa tu conexión y el espacio disponible.");
        }
        finally
        {
            _prepareGate.Release();
        }
    }

    public async Task<VoiceStartResult> StartListeningAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsReady)
        {
            var preparation = await PrepareAsync(cancellationToken: cancellationToken);
            if (!preparation.IsReady)
            {
                return VoiceStartResult.Unavailable(preparation.Detail);
            }
        }

        lock (_sync)
        {
            if (IsListening)
            {
                return VoiceStartResult.Started(
                    "Whisper local · base · español");
            }
        }

        try
        {
            Directory.CreateDirectory(_temporaryDirectory);
            var recordingPath = Path.Combine(
                _temporaryDirectory,
                $"voice-{Guid.NewGuid():N}.wav");

            var recorder = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 100,
                NumberOfBuffers = 3
            };
            var writer = new WaveFileWriter(recordingPath, recorder.WaveFormat);
            var stopped = new TaskCompletionSource<Exception?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            recorder.DataAvailable += Recorder_DataAvailable;
            recorder.RecordingStopped += Recorder_RecordingStopped;

            lock (_sync)
            {
                _recorder = recorder;
                _writer = writer;
                _recordingStopped = stopped;
                _recordingPath = recordingPath;
                _recordedBytes = 0;
                IsListening = true;
            }

            recorder.StartRecording();
            return VoiceStartResult.Started(
                "Whisper local · base · español");
        }
        catch (Exception exception) when (
            exception is MmException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            CleanupRecorder(deleteRecording: true);
            return VoiceStartResult.Unavailable(
                "No pude abrir el micrófono. Revisa el dispositivo de entrada y los permisos de Windows.");
        }
    }

    public async Task<VoiceRecognitionResult> StopListeningAsync(
        CancellationToken cancellationToken = default)
    {
        WaveInEvent? recorder;
        Task<Exception?> stoppedTask;

        lock (_sync)
        {
            if (!IsListening || _recorder is null)
            {
                return VoiceRecognitionResult.NoSpeech("Nexo no estaba escuchando.");
            }

            IsListening = false;
            recorder = _recorder;
            stoppedTask = _recordingStopped?.Task
                ?? Task.FromResult<Exception?>(null);
        }

        try
        {
            recorder.StopRecording();
            await Task.WhenAny(
                stoppedTask,
                Task.Delay(RecordingStopTimeout, cancellationToken));

            var recordingError = stoppedTask.IsCompletedSuccessfully
                ? await stoppedTask
                : null;

            string? recordingPath;
            long recordedBytes;
            lock (_sync)
            {
                recordingPath = _recordingPath;
                recordedBytes = _recordedBytes;
            }

            CleanupRecorder(deleteRecording: false);

            if (recordingError is not null)
            {
                TryDeleteFile(recordingPath);
                return VoiceRecognitionResult.NoSpeech(
                    "Windows interrumpió la grabación. Revisa el micrófono e inténtalo otra vez.");
            }

            if (string.IsNullOrWhiteSpace(recordingPath) ||
                !File.Exists(recordingPath) ||
                recordedBytes < MinimumRecordedBytes)
            {
                TryDeleteFile(recordingPath);
                return VoiceRecognitionResult.NoSpeech(
                    "No detecté suficiente audio. Mantén Mic presionado mientras terminas de hablar.");
            }

            try
            {
                return await TranscribeAsync(recordingPath, cancellationToken);
            }
            finally
            {
                TryDeleteFile(recordingPath);
            }
        }
        catch (OperationCanceledException)
        {
            await CancelAsync();
            throw;
        }
        catch (Exception exception) when (
            exception is MmException or InvalidOperationException or IOException)
        {
            CleanupRecorder(deleteRecording: true);
            return VoiceRecognitionResult.NoSpeech(
                "No pude completar la grabación. Inténtalo otra vez.");
        }
    }

    public async Task<VoiceRecognitionResult> ListenForUtteranceAsync(
        TimeSpan maximumDuration,
        TimeSpan trailingSilence,
        CancellationToken cancellationToken = default)
    {
        if (maximumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        }

        if (trailingSilence < TimeSpan.FromMilliseconds(300))
        {
            throw new ArgumentOutOfRangeException(nameof(trailingSilence));
        }

        var utteranceCompleted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_sync)
        {
            _automaticUtteranceCompleted = utteranceCompleted;
            _automaticTrailingSilence = trailingSilence;
            _automaticSilentMilliseconds = 0;
            _automaticSpeechDetected = false;
            _automaticListening = true;
        }

        var start = await StartListeningAsync(cancellationToken);
        if (!start.IsAvailable)
        {
            ResetAutomaticListening();
            return VoiceRecognitionResult.NoSpeech(start.Detail);
        }

        try
        {
            var timeoutTask = Task.Delay(maximumDuration, cancellationToken);
            await Task.WhenAny(utteranceCompleted.Task, timeoutTask);
            cancellationToken.ThrowIfCancellationRequested();

            bool speechDetected;
            lock (_sync)
            {
                speechDetected = _automaticSpeechDetected;
            }

            if (!speechDetected)
            {
                await CancelAsync();
                return VoiceRecognitionResult.NoSpeech(
                    "No escuché una orden después de activarme.");
            }

            return await StopListeningAsync(cancellationToken);
        }
        finally
        {
            ResetAutomaticListening();
        }
    }

    public async Task CancelAsync()
    {
        WaveInEvent? recorder;
        Task completionTask;

        lock (_sync)
        {
            recorder = _recorder;
            completionTask = _recordingStopped?.Task ?? Task.CompletedTask;
            IsListening = false;
        }

        if (recorder is not null)
        {
            try
            {
                recorder.StopRecording();
                await Task.WhenAny(completionTask, Task.Delay(400));
            }
            catch (MmException)
            {
                // El dispositivo ya se había detenido.
            }
        }

        CleanupRecorder(deleteRecording: true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _recorder?.StopRecording();
        }
        catch (MmException)
        {
            // El micrófono ya no estaba activo.
        }

        CleanupRecorder(deleteRecording: true);
        GC.SuppressFinalize(this);
    }

    private async Task<VoiceRecognitionResult> TranscribeAsync(
        string recordingPath,
        CancellationToken cancellationToken)
    {
        try
        {
            using var whisperFactory = WhisperFactory.FromPath(_modelPath);
            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage("es")
                .WithPrompt(CommandPrompt)
                .Build();
            await using var audioStream = File.OpenRead(recordingPath);

            var transcript = new StringBuilder();
            await foreach (var segment in processor.ProcessAsync(
                audioStream,
                cancellationToken))
            {
                var text = segment.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (transcript.Length > 0)
                {
                    transcript.Append(' ');
                }

                transcript.Append(text);
            }

            var normalizedText = SpanishVoiceTranscriptNormalizer.Normalize(
                transcript.ToString());
            return string.IsNullOrWhiteSpace(normalizedText)
                ? VoiceRecognitionResult.NoSpeech(
                    "Whisper no encontró una orden clara. Habla un poco más cerca del micrófono.")
                : VoiceRecognitionResult.Recognized(normalizedText, 0.95);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return VoiceRecognitionResult.NoSpeech(
                "No pude transcribir el audio localmente. Reinicia Nexo e inténtalo otra vez.");
        }
    }

    private void Recorder_DataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_sync)
        {
            if (_writer is null || e.BytesRecorded <= 0)
            {
                return;
            }

            _writer.Write(e.Buffer, 0, e.BytesRecorded);
            _recordedBytes += e.BytesRecorded;

            if (_automaticListening)
            {
                ObserveAutomaticUtterance(e.Buffer, e.BytesRecorded);
            }
        }
    }


    private void ObserveAutomaticUtterance(byte[] buffer, int bytesRecorded)
    {
        if (_recorder is null || bytesRecorded < 2)
        {
            return;
        }

        long amplitudeTotal = 0;
        var sampleCount = 0;

        for (var index = 0; index + 1 < bytesRecorded; index += 2)
        {
            var sample = (short)(buffer[index] | (buffer[index + 1] << 8));
            amplitudeTotal += Math.Abs((int)sample);
            sampleCount++;
        }

        if (sampleCount == 0)
        {
            return;
        }

        var averageAmplitude = amplitudeTotal / sampleCount;
        var bufferMilliseconds = Math.Max(1,
            bytesRecorded * 1000 / _recorder.WaveFormat.AverageBytesPerSecond);

        if (averageAmplitude >= SpeechAverageAmplitudeThreshold)
        {
            _automaticSpeechDetected = true;
            _automaticSilentMilliseconds = 0;
            return;
        }

        if (!_automaticSpeechDetected)
        {
            return;
        }

        _automaticSilentMilliseconds += bufferMilliseconds;
        if (_automaticSilentMilliseconds >= _automaticTrailingSilence.TotalMilliseconds)
        {
            _automaticUtteranceCompleted?.TrySetResult(true);
        }
    }

    private void ResetAutomaticListening()
    {
        lock (_sync)
        {
            _automaticUtteranceCompleted = null;
            _automaticTrailingSilence = TimeSpan.Zero;
            _automaticSilentMilliseconds = 0;
            _automaticSpeechDetected = false;
            _automaticListening = false;
        }
    }

    private void Recorder_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        _recordingStopped?.TrySetResult(e.Exception);
    }

    private void CleanupRecorder(bool deleteRecording)
    {
        WaveInEvent? recorder;
        WaveFileWriter? writer;
        string? recordingPath;

        lock (_sync)
        {
            recorder = _recorder;
            writer = _writer;
            recordingPath = _recordingPath;

            _recorder = null;
            _writer = null;
            _recordingStopped = null;
            _recordingPath = null;
            _recordedBytes = 0;
            IsListening = false;
        }

        if (recorder is not null)
        {
            recorder.DataAvailable -= Recorder_DataAvailable;
            recorder.RecordingStopped -= Recorder_RecordingStopped;
            recorder.Dispose();
        }

        writer?.Dispose();

        if (deleteRecording)
        {
            TryDeleteFile(recordingPath);
        }
    }

    private bool IsUsableModelFile()
    {
        try
        {
            return File.Exists(_modelPath) &&
                   new FileInfo(_modelPath).Length >= MinimumModelBytes;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // El archivo temporal se limpiará en el siguiente inicio.
        }
        catch (UnauthorizedAccessException)
        {
            // No se interrumpe Nexo por un archivo temporal bloqueado.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
