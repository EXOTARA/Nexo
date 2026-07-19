using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using NAudio;
using NAudio.Wave;
using Nexo.Core.Voice;
using Vosk;

namespace Nexo.Windows.Voice;

/// <summary>
/// Detector opcional de la frase de activación. Vosk solo escucha un vocabulario
/// pequeño y Whisper sigue encargado de transcribir la orden completa.
/// </summary>
public sealed class VoskWakeWordService : IWakeWordService
{
    private const string ModelName = "vosk-model-small-es-0.42";
    private const string ModelUrl =
        "https://alphacephei.com/vosk/models/vosk-model-small-es-0.42.zip";
    private const int SampleRate = 16_000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;
    private const int PreRollMilliseconds = 2_200;
    private const int PreRollCapacityBytes =
        SampleRate * Channels * (BitsPerSample / 8) * PreRollMilliseconds / 1000;
    private const long MinimumArchiveBytes = 20L * 1024 * 1024;
    private const long ProgressStepBytes = 2L * 1024 * 1024;

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DetectionCooldown = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan HandoffDelay = TimeSpan.FromMilliseconds(260);
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(20)
    };

    private readonly object _sync = new();
    private readonly SemaphoreSlim _prepareGate = new(1, 1);
    private readonly string _modelDirectory;
    private readonly string _modelsRoot;
    private readonly byte[] _preRollBuffer = new byte[PreRollCapacityBytes];

    private Model? _model;
    private VoskRecognizer? _recognizer;
    private WaveInEvent? _recorder;
    private TaskCompletionSource<Exception?>? _recordingStopped;
    private WakeWordPhrase _phrase;
    private DateTimeOffset _lastDetection = DateTimeOffset.MinValue;
    private bool _detectionRaised;
    private int _preRollWriteIndex;
    private int _preRollCount;
    private CancellationTokenSource? _handoffCancellation;
    private bool _disposed;

    public VoskWakeWordService()
    {
        _modelsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexo",
            "Models",
            "Vosk");
        _modelDirectory = Path.Combine(_modelsRoot, ModelName);
        IsReady = IsUsableModelDirectory();
    }

    public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

    public bool IsReady { get; private set; }

    public bool IsListening { get; private set; }

    public int InputDeviceNumber { get; set; } = -1;

    public async Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsUsableModelDirectory())
        {
            IsReady = true;
            return VoicePreparationResult.Ready(
                "Activación local lista · Vosk español");
        }

        await _prepareGate.WaitAsync(cancellationToken);
        try
        {
            if (IsUsableModelDirectory())
            {
                IsReady = true;
                return VoicePreparationResult.Ready(
                    "Activación local lista · Vosk español");
            }

            progress?.Report(VoicePreparationProgress.Preparing(
                "Preparando la activación por “Nexo”…"));

            Directory.CreateDirectory(_modelsRoot);
            var archivePath = Path.Combine(_modelsRoot, ModelName + ".zip.download");
            var stagingDirectory = Path.Combine(
                _modelsRoot,
                ".extract-" + Guid.NewGuid().ToString("N"));

            TryDeleteFile(archivePath);
            TryDeleteDirectory(stagingDirectory);

            long downloadedBytes = 0;
            using (var response = await HttpClient.GetAsync(
                ModelUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var destination = new FileStream(
                    archivePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81_920,
                    useAsync: true);

                var buffer = new byte[81_920];
                long lastReportedBytes = 0;

                while (true)
                {
                    var bytesRead = await source.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(
                        buffer.AsMemory(0, bytesRead),
                        cancellationToken);
                    downloadedBytes += bytesRead;

                    if (downloadedBytes - lastReportedBytes >= ProgressStepBytes)
                    {
                        lastReportedBytes = downloadedBytes;
                        progress?.Report(VoicePreparationProgress.Downloading(downloadedBytes));
                    }
                }

                await destination.FlushAsync(cancellationToken);
            }

            if (downloadedBytes < MinimumArchiveBytes)
            {
                TryDeleteFile(archivePath);
                return VoicePreparationResult.Unavailable(
                    "La descarga del detector quedó incompleta. Revisa tu conexión.");
            }

            progress?.Report(VoicePreparationProgress.Preparing(
                "Instalando el detector local de la palabra Nexo…"));

            Directory.CreateDirectory(stagingDirectory);
            ZipFile.ExtractToDirectory(archivePath, stagingDirectory, overwriteFiles: true);

            var extractedDirectory = FindExtractedModelDirectory(stagingDirectory);
            if (extractedDirectory is null)
            {
                TryDeleteFile(archivePath);
                TryDeleteDirectory(stagingDirectory);
                return VoicePreparationResult.Unavailable(
                    "El modelo descargado no tiene el formato esperado.");
            }

            TryDeleteDirectory(_modelDirectory);
            Directory.Move(extractedDirectory, _modelDirectory);
            TryDeleteDirectory(stagingDirectory);
            TryDeleteFile(archivePath);

            IsReady = IsUsableModelDirectory();
            return IsReady
                ? VoicePreparationResult.Ready(
                    "Activación local lista · Vosk español")
                : VoicePreparationResult.Unavailable(
                    "No pude terminar de instalar el detector local.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            IsReady = false;
            return VoicePreparationResult.Unavailable(
                "No pude preparar la activación por voz. Revisa tu conexión y el espacio disponible.");
        }
        finally
        {
            _prepareGate.Release();
        }
    }

    public async Task<VoiceStartResult> StartListeningAsync(
        WakeWordPhrase phrase,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (IsListening)
        {
            return VoiceStartResult.Started(
                $"esperando “{phrase.ToSpokenText()}”");
        }

        if (!IsReady)
        {
            var preparation = await PrepareAsync(cancellationToken: cancellationToken);
            if (!preparation.IsReady)
            {
                return VoiceStartResult.Unavailable(preparation.Detail);
            }
        }

        Model? model = null;
        VoskRecognizer? recognizer = null;
        WaveInEvent? recorder = null;
        var ownershipTransferred = false;

        try
        {
            Vosk.Vosk.SetLogLevel(-1);

            model = new Model(_modelDirectory);
            recognizer = new VoskRecognizer(
                model,
                SampleRate,
                BuildGrammar(phrase));
            recorder = new WaveInEvent
            {
                DeviceNumber = ResolveInputDeviceNumber(),
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 100,
                NumberOfBuffers = 3
            };
            var stopped = new TaskCompletionSource<Exception?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            recorder.DataAvailable += Recorder_DataAvailable;
            recorder.RecordingStopped += Recorder_RecordingStopped;

            lock (_sync)
            {
                _phrase = phrase;
                _model = model;
                _recognizer = recognizer;
                _recorder = recorder;
                _recordingStopped = stopped;
                _detectionRaised = false;
                _preRollWriteIndex = 0;
                _preRollCount = 0;
                _handoffCancellation?.Cancel();
                _handoffCancellation?.Dispose();
                _handoffCancellation = null;
                IsListening = true;
                ownershipTransferred = true;
            }

            recorder.StartRecording();
            return VoiceStartResult.Started(
                $"esperando “{phrase.ToSpokenText()}”");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            if (ownershipTransferred)
            {
                await StopListeningAsync();
            }
            else
            {
                recorder?.Dispose();
                recognizer?.Dispose();
                model?.Dispose();
            }

            return VoiceStartResult.Unavailable(
                "No pude iniciar la escucha de la palabra Nexo. Revisa el micrófono y la arquitectura de Windows.");
        }
    }

    public async Task StopListeningAsync()
    {
        WaveInEvent? recorder;
        Task<Exception?> stoppedTask;

        lock (_sync)
        {
            recorder = _recorder;
            stoppedTask = _recordingStopped?.Task
                ?? Task.FromResult<Exception?>(null);
            IsListening = false;
        }

        if (recorder is not null)
        {
            try
            {
                recorder.StopRecording();
                await Task.WhenAny(stoppedTask, Task.Delay(StopTimeout));
            }
            catch (MmException)
            {
                // El dispositivo ya se había detenido.
            }
        }

        CleanupRecognizer();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CleanupRecognizer();
        GC.SuppressFinalize(this);
    }

    private void Recorder_DataAvailable(object? sender, WaveInEventArgs e)
    {
        VoskRecognizer? recognizer;
        WakeWordPhrase phrase;
        bool detectionRaised;

        lock (_sync)
        {
            if (!IsListening || e.BytesRecorded <= 0)
            {
                return;
            }

            AppendPreRollUnsafe(e.Buffer, e.BytesRecorded);
            detectionRaised = _detectionRaised;
            recognizer = _recognizer;
            phrase = _phrase;
        }

        if (detectionRaised || recognizer is null)
        {
            return;
        }

        try
        {
            var isFinal = recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded);
            var json = isFinal ? recognizer.Result() : recognizer.PartialResult();
            var recognizedText = ReadRecognizedText(
                json,
                isFinal ? "text" : "partial");

            if (!WakeWordTextMatcher.IsMatch(recognizedText, phrase))
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            CancellationTokenSource handoffCancellation;

            lock (_sync)
            {
                if (_detectionRaised || now - _lastDetection < DetectionCooldown)
                {
                    return;
                }

                _detectionRaised = true;
                _lastDetection = now;
                _handoffCancellation?.Cancel();
                _handoffCancellation?.Dispose();
                _handoffCancellation = new CancellationTokenSource();
                handoffCancellation = _handoffCancellation;
            }

            _ = RaiseWakeWordAfterHandoffAsync(
                phrase,
                recognizedText,
                handoffCancellation.Token);
        }
        catch (ObjectDisposedException)
        {
            // La escucha se detuvo mientras llegaba el último bloque de audio.
        }
    }

    private async Task RaiseWakeWordAfterHandoffAsync(
        WakeWordPhrase phrase,
        string recognizedText,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(HandoffDelay, cancellationToken);

            byte[] preRollAudio;
            lock (_sync)
            {
                if (!IsListening || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                preRollAudio = SnapshotPreRollUnsafe();
            }

            WakeWordDetected?.Invoke(
                this,
                new WakeWordDetectedEventArgs(
                    phrase,
                    recognizedText,
                    preRollAudio));
        }
        catch (OperationCanceledException)
        {
            // La escucha cambió de estado antes del traspaso.
        }
    }

    private void AppendPreRollUnsafe(byte[] source, int count)
    {
        var sourceOffset = 0;
        var remaining = Math.Min(count, source.Length);

        while (remaining > 0)
        {
            var writable = Math.Min(
                remaining,
                _preRollBuffer.Length - _preRollWriteIndex);
            Buffer.BlockCopy(
                source,
                sourceOffset,
                _preRollBuffer,
                _preRollWriteIndex,
                writable);

            _preRollWriteIndex =
                (_preRollWriteIndex + writable) % _preRollBuffer.Length;
            _preRollCount = Math.Min(
                _preRollBuffer.Length,
                _preRollCount + writable);
            sourceOffset += writable;
            remaining -= writable;
        }
    }

    private byte[] SnapshotPreRollUnsafe()
    {
        if (_preRollCount == 0)
        {
            return [];
        }

        var result = new byte[_preRollCount];
        var start = (_preRollWriteIndex - _preRollCount + _preRollBuffer.Length) %
            _preRollBuffer.Length;
        var firstLength = Math.Min(
            _preRollCount,
            _preRollBuffer.Length - start);

        Buffer.BlockCopy(
            _preRollBuffer,
            start,
            result,
            0,
            firstLength);

        if (firstLength < _preRollCount)
        {
            Buffer.BlockCopy(
                _preRollBuffer,
                0,
                result,
                firstLength,
                _preRollCount - firstLength);
        }

        return result;
    }

    private void Recorder_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        _recordingStopped?.TrySetResult(e.Exception);
    }

    private void CleanupRecognizer()
    {
        WaveInEvent? recorder;
        VoskRecognizer? recognizer;
        Model? model;

        lock (_sync)
        {
            recorder = _recorder;
            recognizer = _recognizer;
            model = _model;

            _recorder = null;
            _recognizer = null;
            _model = null;
            _recordingStopped = null;
            _detectionRaised = false;
            _preRollWriteIndex = 0;
            _preRollCount = 0;
            _handoffCancellation?.Cancel();
            _handoffCancellation?.Dispose();
            _handoffCancellation = null;
            IsListening = false;
        }

        if (recorder is not null)
        {
            recorder.DataAvailable -= Recorder_DataAvailable;
            recorder.RecordingStopped -= Recorder_RecordingStopped;
            recorder.Dispose();
        }

        recognizer?.Dispose();
        model?.Dispose();
    }

    private int ResolveInputDeviceNumber()
    {
        var deviceCount = WaveIn.DeviceCount;
        if (deviceCount <= 0)
        {
            throw new InvalidOperationException(
                "Windows no encontró micrófonos disponibles.");
        }

        return InputDeviceNumber >= 0 && InputDeviceNumber < deviceCount
            ? InputDeviceNumber
            : 0;
    }

    private bool IsUsableModelDirectory()
    {
        try
        {
            return Directory.Exists(_modelDirectory) &&
                   File.Exists(Path.Combine(_modelDirectory, "am", "final.mdl")) &&
                   File.Exists(Path.Combine(_modelDirectory, "conf", "mfcc.conf"));
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

    private static string? FindExtractedModelDirectory(string stagingDirectory)
    {
        var direct = Path.Combine(stagingDirectory, ModelName);
        if (Directory.Exists(direct))
        {
            return direct;
        }

        return Directory
            .EnumerateDirectories(stagingDirectory)
            .FirstOrDefault(directory =>
                File.Exists(Path.Combine(directory, "am", "final.mdl")));
    }

    private static string BuildGrammar(WakeWordPhrase phrase) => phrase switch
    {
        WakeWordPhrase.OyeNexo => "[\"oye nexo\", \"[unk]\"]",
        _ => "[\"nexo\", \"oye nexo\", \"[unk]\"]"
    };

    private static string ReadRecognizedText(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(propertyName, out var value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Se intentará limpiar en la siguiente preparación.
        }
        catch (UnauthorizedAccessException)
        {
            // No se cierra Nexo por un archivo de descarga bloqueado.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Se intentará limpiar en la siguiente preparación.
        }
        catch (UnauthorizedAccessException)
        {
            // No se cierra Nexo por una carpeta temporal bloqueada.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
