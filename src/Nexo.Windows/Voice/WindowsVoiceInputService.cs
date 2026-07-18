using System.Globalization;
using System.Speech.Recognition;
using Nexo.Core.Voice;

namespace Nexo.Windows.Voice;

public sealed class WindowsVoiceInputService : IVoiceInputService
{
    private const string CommandGrammarName = "NexoSpanishCommands";
    private const string DictationGrammarName = "NexoSpanishDictation";
    private const double MinimumCommandConfidence = 0.42;
    private const double MinimumDictationConfidence = 0.60;

    private static readonly TimeSpan ReleaseTailDelay = TimeSpan.FromMilliseconds(350);

    private readonly object _sync = new();

    private SpeechRecognitionEngine? _engine;
    private TaskCompletionSource<bool>? _recognitionCompleted;
    private string _bestText = string.Empty;
    private double _bestConfidence;
    private bool _bestResultUsesCommandGrammar;
    private bool _disposed;

    public bool IsListening { get; private set; }

    public Task<VoiceStartResult> StartListeningAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_disposed)
            {
                return Task.FromResult(VoiceStartResult.Unavailable(
                    "El servicio de voz ya fue cerrado."));
            }

            if (IsListening)
            {
                return Task.FromResult(VoiceStartResult.Started(
                    GetRecognizerLabel(_engine?.RecognizerInfo)));
            }
        }

        try
        {
            var recognizer = SelectSpanishRecognizer();
            if (recognizer is null)
            {
                return Task.FromResult(VoiceStartResult.Unavailable(
                    "No encontré un reconocedor de español instalado. Instala la característica de voz de Español (México) en Windows."));
            }

            var engine = new SpeechRecognitionEngine(recognizer.Id)
            {
                InitialSilenceTimeout = TimeSpan.FromSeconds(5),
                BabbleTimeout = TimeSpan.FromSeconds(8),
                EndSilenceTimeout = TimeSpan.FromMilliseconds(550),
                EndSilenceTimeoutAmbiguous = TimeSpan.FromMilliseconds(900),
                MaxAlternates = 5
            };

            var commandBuilder = new GrammarBuilder(
                new Choices(SpanishVoiceCommandCatalog.CreatePhrases().ToArray()))
            {
                Culture = recognizer.Culture
            };

            var commandGrammar = new Grammar(commandBuilder)
            {
                Name = CommandGrammarName
            };

            var dictationGrammar = new DictationGrammar
            {
                Name = DictationGrammarName
            };

            engine.LoadGrammar(commandGrammar);
            engine.LoadGrammar(dictationGrammar);
            engine.SetInputToDefaultAudioDevice();
            engine.SpeechRecognized += Engine_SpeechRecognized;
            engine.RecognizeCompleted += Engine_RecognizeCompleted;

            lock (_sync)
            {
                _bestText = string.Empty;
                _bestConfidence = 0;
                _bestResultUsesCommandGrammar = false;
                _recognitionCompleted = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _engine = engine;
                IsListening = true;
            }

            engine.RecognizeAsync(RecognizeMode.Multiple);
            return Task.FromResult(VoiceStartResult.Started(
                GetRecognizerLabel(recognizer)));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            DisposeEngine();
            return Task.FromResult(VoiceStartResult.Unavailable(
                "No pude iniciar el reconocimiento. Revisa el micrófono y el idioma de voz de Windows."));
        }
    }

    public async Task<VoiceRecognitionResult> StopListeningAsync(
        CancellationToken cancellationToken = default)
    {
        SpeechRecognitionEngine? engine;
        Task completionTask;

        lock (_sync)
        {
            if (!IsListening || _engine is null)
            {
                return VoiceRecognitionResult.NoSpeech("Nexo no estaba escuchando.");
            }

            engine = _engine;
            completionTask = _recognitionCompleted?.Task ?? Task.CompletedTask;
            IsListening = false;
        }

        try
        {
            // Evita cortar la última sílaba cuando el usuario suelta Mic inmediatamente.
            await Task.Delay(ReleaseTailDelay, cancellationToken);
            engine.RecognizeAsyncStop();
            await Task.WhenAny(
                completionTask,
                Task.Delay(TimeSpan.FromSeconds(2.5), cancellationToken));
        }
        catch (OperationCanceledException)
        {
            await CancelAsync();
            throw;
        }
        catch (InvalidOperationException)
        {
            // El motor puede haber finalizado antes de recibir la orden de detenerse.
        }

        string text;
        double confidence;
        bool isCommandGrammar;

        lock (_sync)
        {
            text = _bestText;
            confidence = _bestConfidence;
            isCommandGrammar = _bestResultUsesCommandGrammar;
        }

        DisposeEngine();

        var requiredConfidence = isCommandGrammar
            ? MinimumCommandConfidence
            : MinimumDictationConfidence;

        return string.IsNullOrWhiteSpace(text) || confidence < requiredConfidence
            ? VoiceRecognitionResult.NoSpeech(
                "No detecté una orden clara. Habla cerca del micrófono y evita soltar Mic antes de terminar la última palabra.")
            : VoiceRecognitionResult.Recognized(text, confidence);
    }

    public async Task CancelAsync()
    {
        SpeechRecognitionEngine? engine;
        Task completionTask;

        lock (_sync)
        {
            engine = _engine;
            completionTask = _recognitionCompleted?.Task ?? Task.CompletedTask;
            IsListening = false;
        }

        if (engine is null)
        {
            return;
        }

        try
        {
            engine.RecognizeAsyncCancel();
            await Task.WhenAny(completionTask, Task.Delay(400));
        }
        catch (InvalidOperationException)
        {
            // Ya no había una operación activa.
        }
        finally
        {
            DisposeEngine();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeEngine();
        GC.SuppressFinalize(this);
    }

    private void Engine_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Result.Text))
        {
            return;
        }

        var isCommandGrammar = string.Equals(
            e.Result.Grammar?.Name,
            CommandGrammarName,
            StringComparison.Ordinal);

        lock (_sync)
        {
            var shouldReplace = isCommandGrammar
                ? !_bestResultUsesCommandGrammar || e.Result.Confidence >= _bestConfidence
                : !_bestResultUsesCommandGrammar && e.Result.Confidence >= _bestConfidence;

            if (!shouldReplace)
            {
                return;
            }

            _bestText = e.Result.Text.Trim();
            _bestConfidence = e.Result.Confidence;
            _bestResultUsesCommandGrammar = isCommandGrammar;
        }
    }

    private void Engine_RecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        _recognitionCompleted?.TrySetResult(true);
    }

    private void DisposeEngine()
    {
        SpeechRecognitionEngine? engine;

        lock (_sync)
        {
            engine = _engine;
            _engine = null;
            IsListening = false;
            _recognitionCompleted = null;
        }

        if (engine is null)
        {
            return;
        }

        engine.SpeechRecognized -= Engine_SpeechRecognized;
        engine.RecognizeCompleted -= Engine_RecognizeCompleted;

        try
        {
            engine.SetInputToNull();
        }
        catch (InvalidOperationException)
        {
            // El dispositivo ya fue liberado.
        }

        engine.Dispose();
    }

    private static RecognizerInfo? SelectSpanishRecognizer()
    {
        var installed = SpeechRecognitionEngine.InstalledRecognizers();
        if (installed.Count == 0)
        {
            return null;
        }

        return installed.FirstOrDefault(item =>
                   item.Culture.Name.Equals("es-MX", StringComparison.OrdinalIgnoreCase))
               ?? installed.FirstOrDefault(item =>
                   item.Culture.Name.Equals("es-ES", StringComparison.OrdinalIgnoreCase))
               ?? installed.FirstOrDefault(item =>
                   item.Culture.TwoLetterISOLanguageName.Equals(
                       "es",
                       StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRecognizerLabel(RecognizerInfo? recognizer)
    {
        if (recognizer is null)
        {
            return "Reconocedor de español de Windows";
        }

        return $"{recognizer.Name} · {recognizer.Culture.Name}";
    }
}
