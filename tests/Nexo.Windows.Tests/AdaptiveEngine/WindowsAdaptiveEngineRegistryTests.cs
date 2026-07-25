using Nexo.Core.Ai;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Settings;
using Nexo.Windows.AdaptiveEngine;
using Nexo.Windows.Tests.Voice;
using Nexo.Windows.Voice;

namespace Nexo.Windows.Tests.AdaptiveEngine;

public sealed class WindowsAdaptiveEngineRegistryTests
{
    [Fact]
    public void GetDescriptors_RegistersWhisperVoskAndSapiExactlyOnce()
    {
        var registry = CreateRegistry(out _, out _, out _);

        var descriptors = registry.GetDescriptors();

        Assert.Single(descriptors, d => d.Category == EngineCategory.SpeechToText && d.DisplayName.Contains("Whisper"));
        Assert.Single(descriptors, d => d.Category == EngineCategory.WakeWord && d.DisplayName.Contains("Vosk"));
        Assert.Single(descriptors, d => d.Category == EngineCategory.TextToSpeech && d.DisplayName.Contains("SAPI"));
    }

    [Fact]
    public void GetDescriptors_HaveUniqueIdentifiers()
    {
        var registry = CreateRegistry(out _, out _, out _);

        var descriptors = registry.GetDescriptors();

        Assert.Equal(descriptors.Count, descriptors.Select(d => d.Id).Distinct().Count());
    }

    [Fact]
    public void GetDescriptors_AreStableAcrossMultipleCalls()
    {
        var registry = CreateRegistry(out _, out _, out _);

        var first = registry.GetDescriptors();
        var second = registry.GetDescriptors();

        Assert.Equal(first.Select(d => d.Id), second.Select(d => d.Id));
    }

    [Fact]
    public void GetDescriptors_DoesNotRegisterAnyUnintegratedFutureEngine()
    {
        var registry = CreateRegistry(out _, out _, out _);

        var names = registry.GetDescriptors().Select(d => d.DisplayName).ToList();

        Assert.DoesNotContain(names, n => n.Contains("openWakeWord", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Silero", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Kokoro", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Piper", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("OCR", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetDescriptors_RemoteProvider_IsNotMarkedLocal()
    {
        var registry = CreateRegistry(out _, out _, out _);

        var openAi = registry.GetDescriptors().Single(d => d.DisplayName.Contains("OpenAI (remoto)"));

        Assert.False(openAi.IsLocal);
    }

    [Fact]
    public void GetDescriptors_LocalLanguageModelProviders_AreMarkedLocal()
    {
        var registry = CreateRegistry(out _, out _, out _);

        var ollama = registry.GetDescriptors().Single(d => d.DisplayName.Contains("Ollama"));
        var lmStudio = registry.GetDescriptors().Single(d => d.DisplayName.Contains("LM Studio"));

        Assert.True(ollama.IsLocal);
        Assert.True(lmStudio.IsLocal);
    }

    [Fact]
    public void CaptureRuntimeStates_ReflectsVoiceCoordinatorReadyAndListeningState()
    {
        var registry = CreateRegistry(out var voiceInput, out var wakeWord, out _);
        voiceInput.IsReady = true;
        voiceInput.IsListening = true;
        wakeWord.IsReady = false;
        wakeWord.IsListening = false;

        var preferences = new ShellPreferences { WakeWordEnabled = true };
        var states = registry.CaptureRuntimeStates(preferences, ollamaRuntimeSnapshot: null);

        var whisperState = states.Single(s => s.EngineId == WhisperId(registry));
        var voskState = states.Single(s => s.EngineId == VoskId(registry));

        Assert.True(whisperState.IsAvailable);
        Assert.True(whisperState.IsActive);
        Assert.False(voskState.IsAvailable);
        Assert.False(voskState.IsActive);
        Assert.True(voskState.IsConfigured);
    }

    [Fact]
    public void CaptureRuntimeStates_WakeWordDisabledInPreferences_IsNotConfigured()
    {
        var registry = CreateRegistry(out _, out _, out _);
        var preferences = new ShellPreferences { WakeWordEnabled = false };

        var states = registry.CaptureRuntimeStates(preferences, ollamaRuntimeSnapshot: null);

        var voskState = states.Single(s => s.EngineId == VoskId(registry));
        Assert.False(voskState.IsConfigured);
    }

    [Fact]
    public void CaptureRuntimeStates_OllamaConfigured_ButNoSnapshot_DoesNotClaimActive()
    {
        var registry = CreateRegistry(out _, out _, out _);
        var preferences = new ShellPreferences { AiProvider = AiProviderKind.Ollama };

        var states = registry.CaptureRuntimeStates(preferences, ollamaRuntimeSnapshot: null);

        var ollamaState = states.Single(s => s.EngineId == OllamaId(registry));
        Assert.True(ollamaState.IsConfigured);
        Assert.Null(ollamaState.IsActive);
    }

    [Fact]
    public void CaptureRuntimeStates_OllamaConfiguredAndRunning_IsActive()
    {
        var registry = CreateRegistry(out _, out _, out _);
        var preferences = new ShellPreferences { AiProvider = AiProviderKind.Ollama };
        var snapshot = new OllamaRuntimeSnapshot(
            OllamaRuntimeState.ManagedRunning, "http://localhost:11435", "C:\\ollama.exe", "Ollama listo");

        var states = registry.CaptureRuntimeStates(preferences, snapshot);

        var ollamaState = states.Single(s => s.EngineId == OllamaId(registry));
        Assert.True(ollamaState.IsConfigured);
        Assert.True(ollamaState.IsAvailable);
        Assert.True(ollamaState.IsActive);
    }

    [Fact]
    public void CaptureRuntimeStates_OllamaConfiguredButNotRunning_IsNotActive()
    {
        var registry = CreateRegistry(out _, out _, out _);
        var preferences = new ShellPreferences { AiProvider = AiProviderKind.Ollama };
        var snapshot = new OllamaRuntimeSnapshot(
            OllamaRuntimeState.ManagedInstalled, "http://localhost:11435", "C:\\ollama.exe", "Instalado, no iniciado");

        var states = registry.CaptureRuntimeStates(preferences, snapshot);

        var ollamaState = states.Single(s => s.EngineId == OllamaId(registry));
        Assert.True(ollamaState.IsConfigured);
        Assert.False(ollamaState.IsActive);
    }

    [Fact]
    public void CaptureRuntimeStates_OllamaSelectedButOtherProviderIsNotConfigured()
    {
        var registry = CreateRegistry(out _, out _, out _);
        var preferences = new ShellPreferences { AiProvider = AiProviderKind.Ollama };

        var states = registry.CaptureRuntimeStates(preferences, ollamaRuntimeSnapshot: null);

        var openAiState = states.Single(s => s.EngineId == OpenAiId(registry));
        Assert.False(openAiState.IsConfigured);
    }

    [Fact]
    public void CaptureRuntimeStates_ProducesNoDuplicateEngineIds()
    {
        var registry = CreateRegistry(out _, out _, out _);
        var preferences = new ShellPreferences();

        var states = registry.CaptureRuntimeStates(preferences, ollamaRuntimeSnapshot: null);

        Assert.Equal(states.Count, states.Select(s => s.EngineId).Distinct().Count());
    }

    private static WindowsAdaptiveEngineRegistry CreateRegistry(
        out FakeVoiceInputService voiceInput,
        out FakeWakeWordService wakeWord,
        out FakeVoiceOutputService voiceOutput)
    {
        var log = new VoiceCallLog();
        voiceInput = new FakeVoiceInputService(log);
        voiceOutput = new FakeVoiceOutputService(log);
        wakeWord = new FakeWakeWordService(log);
        var coordinator = new VoiceCoordinator(voiceInput, voiceOutput, wakeWord);
        return new WindowsAdaptiveEngineRegistry(coordinator);
    }

    private static EngineIdentifier WhisperId(WindowsAdaptiveEngineRegistry registry) =>
        registry.GetDescriptors().Single(d => d.Category == EngineCategory.SpeechToText).Id;

    private static EngineIdentifier VoskId(WindowsAdaptiveEngineRegistry registry) =>
        registry.GetDescriptors().Single(d => d.Category == EngineCategory.WakeWord).Id;

    private static EngineIdentifier OllamaId(WindowsAdaptiveEngineRegistry registry) =>
        registry.GetDescriptors().Single(d => d.DisplayName.Contains("Ollama")).Id;

    private static EngineIdentifier OpenAiId(WindowsAdaptiveEngineRegistry registry) =>
        registry.GetDescriptors().Single(d => d.DisplayName.Contains("OpenAI (remoto)")).Id;
}
