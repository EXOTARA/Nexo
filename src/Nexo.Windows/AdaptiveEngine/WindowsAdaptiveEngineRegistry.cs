using Nexo.Core.Ai;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Settings;
using Nexo.Windows.Voice;

namespace Nexo.Windows.AdaptiveEngine;

public sealed class WindowsAdaptiveEngineRegistry : IAdaptiveEngineRegistry
{
    /// <summary>
    /// Un motor puede servir a más de un proveedor: el motor de Ollama es el mismo tanto si Sakura
    /// lo administra como si la instalación es propia, y los proveedores de nube compatibles con
    /// OpenAI comparten un único cliente. Mapear a un solo proveedor dejaba a la mitad de las
    /// opciones informando "no configurado" mientras estaban funcionando.
    /// </summary>
    private static readonly IReadOnlyDictionary<EngineIdentifier, AiProviderKind[]> AiProvidersByEngineId =
        new Dictionary<EngineIdentifier, AiProviderKind[]>
        {
            [KnownEngineIds.OpenAiLanguageModel] = [AiProviderKind.OpenAI],
            [KnownEngineIds.OllamaLanguageModel] =
                [AiProviderKind.Ollama, AiProviderKind.SakuraLocal],
            [KnownEngineIds.LmStudioLanguageModel] = [AiProviderKind.LMStudio],
            [KnownEngineIds.OpenAiCompatibleLanguageModel] =
            [
                AiProviderKind.OpenAICompatible,
                AiProviderKind.Anthropic,
                AiProviderKind.Groq,
                AiProviderKind.Gemini,
                AiProviderKind.OpenRouter
            ]
        };

    private readonly VoiceCoordinator _voiceCoordinator;
    private readonly IReadOnlyList<EngineDescriptor> _descriptors;

    public WindowsAdaptiveEngineRegistry(VoiceCoordinator voiceCoordinator)
    {
        _voiceCoordinator = voiceCoordinator ?? throw new ArgumentNullException(nameof(voiceCoordinator));
        _descriptors = BuildDescriptors();
    }

    public IReadOnlyList<EngineDescriptor> GetDescriptors() => _descriptors;

    public IReadOnlyList<EngineRuntimeState> CaptureRuntimeStates(
        ShellPreferences preferences,
        OllamaRuntimeSnapshot? ollamaRuntimeSnapshot)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var states = new List<EngineRuntimeState>
        {
            new(
                KnownEngineIds.WhisperSpeechToText,
                IsAvailable: _voiceCoordinator.IsVoiceInputReady,
                IsConfigured: true,
                IsActive: _voiceCoordinator.IsVoiceInputListening,
                Detail: null),
            new(
                KnownEngineIds.VoskWakeWord,
                IsAvailable: _voiceCoordinator.IsWakeWordReady,
                IsConfigured: preferences.WakeWordEnabled,
                IsActive: _voiceCoordinator.IsWakeWordListening,
                Detail: null),
            new(
                KnownEngineIds.WindowsSapiTextToSpeech,
                IsAvailable: true,
                IsConfigured: true,
                IsActive: null,
                Detail: "SAPI no expone si está hablando en este momento.")
        };

        foreach (var (engineId, providerKinds) in AiProvidersByEngineId)
        {
            var isConfigured = preferences.AiProvider != AiProviderKind.Disabled &&
                providerKinds.Contains(preferences.AiProvider);

            bool? isAvailable = null;
            bool? isActive = null;
            string? detail = null;

            if (providerKinds.Any(AiProviderDefaults.UsesOllamaProtocol) &&
                ollamaRuntimeSnapshot is not null)
            {
                isAvailable = ollamaRuntimeSnapshot.State != OllamaRuntimeState.Unavailable;
                isActive = isConfigured && ollamaRuntimeSnapshot.IsRunning;
                detail = ollamaRuntimeSnapshot.Message;
            }

            states.Add(new EngineRuntimeState(engineId, isAvailable, isConfigured, isActive, detail));
        }

        return states;
    }

    private static IReadOnlyList<EngineDescriptor> BuildDescriptors() =>
    [
        new EngineDescriptor(
            KnownEngineIds.WhisperSpeechToText,
            "Whisper (reconocimiento de voz local)",
            EngineCategory.SpeechToText,
            IsLocal: true,
            MinimumRequirement: new EngineRequirement(
                "Procesar audio con el modelo Whisper Base en CPU.",
                EngineCostLevel.Moderate, EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Moderate),
            RecommendedRequirement: new EngineRequirement(
                "Sakura solo ofrece el modelo Whisper Base; no existe un nivel superior configurable.",
                EngineCostLevel.Moderate, EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Moderate),
            Capabilities: ["Transcripción de voz a texto en español", "Modelo Whisper Base fijo"],
            Limitations: ["Ejecuta siempre en CPU: no se comprobó ni se usa GPU", "No se puede elegir un modelo distinto todavía"],
            AllowsRuntimeSelection: false,
            RequiresRestart: true,
            RequiresDownload: true,
            IncludedWithSakura: true),

        new EngineDescriptor(
            KnownEngineIds.VoskWakeWord,
            "Vosk (palabra de activación local)",
            EngineCategory.WakeWord,
            IsLocal: true,
            MinimumRequirement: new EngineRequirement(
                "Detectar la palabra de activación con el modelo Vosk pequeño en español.",
                EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low),
            RecommendedRequirement: new EngineRequirement(
                "Sakura solo ofrece este modelo pequeño en español.",
                EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low),
            Capabilities: ["Detección de palabra de activación en español"],
            Limitations: ["Solo reconoce español", "No se puede elegir un modelo distinto todavía"],
            AllowsRuntimeSelection: false,
            RequiresRestart: true,
            RequiresDownload: false,
            IncludedWithSakura: true),

        new EngineDescriptor(
            KnownEngineIds.WindowsSapiTextToSpeech,
            "Síntesis de voz de Windows (SAPI)",
            EngineCategory.TextToSpeech,
            IsLocal: true,
            MinimumRequirement: new EngineRequirement(
                "Sintetizar voz con las voces instaladas en Windows.",
                EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low),
            RecommendedRequirement: new EngineRequirement(
                "Igual que el mínimo: SAPI no tiene niveles de calidad configurables desde Sakura.",
                EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low),
            Capabilities: ["Síntesis de voz usando las voces instaladas en Windows"],
            Limitations: ["No expone cuál voz eligió ni si está hablando en este momento"],
            AllowsRuntimeSelection: false,
            RequiresRestart: true,
            RequiresDownload: false,
            IncludedWithSakura: true),

        new EngineDescriptor(
            KnownEngineIds.OpenAiLanguageModel,
            "OpenAI (remoto)",
            EngineCategory.LocalLanguageModel,
            IsLocal: false,
            MinimumRequirement: new EngineRequirement(
                "Solo se necesita conexión a internet: el cómputo ocurre en el servidor remoto.",
                EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low),
            RecommendedRequirement: new EngineRequirement(
                "Igual que el mínimo.",
                EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Low),
            Capabilities: ["Modelos de OpenAI accesibles por API"],
            Limitations: ["Requiere clave de API", "Envía datos a un servicio externo"],
            AllowsRuntimeSelection: true,
            RequiresRestart: false,
            RequiresDownload: false,
            IncludedWithSakura: true),

        new EngineDescriptor(
            KnownEngineIds.OllamaLanguageModel,
            "Ollama (modelo de lenguaje local)",
            EngineCategory.LocalLanguageModel,
            IsLocal: true,
            MinimumRequirement: new EngineRequirement(
                "Ejecutar un modelo de lenguaje pequeño localmente.",
                EngineCostLevel.Moderate, EngineCostLevel.High, EngineCostLevel.Unknown, EngineCostLevel.Moderate),
            RecommendedRequirement: new EngineRequirement(
                "Ejecutar modelos más grandes con mejor calidad de respuesta.",
                EngineCostLevel.High, EngineCostLevel.High, EngineCostLevel.Unknown, EngineCostLevel.High),
            Capabilities: ["Modelos de lenguaje ejecutados localmente", "Puede administrarlo Sakura o apuntar a una instalación externa"],
            Limitations: ["El uso de GPU depende de la instalación del usuario y no se puede comprobar desde Sakura", "El modelo debe descargarse por separado"],
            AllowsRuntimeSelection: true,
            RequiresRestart: false,
            RequiresDownload: true,
            IncludedWithSakura: true),

        new EngineDescriptor(
            KnownEngineIds.LmStudioLanguageModel,
            "LM Studio (modelo de lenguaje local)",
            EngineCategory.LocalLanguageModel,
            IsLocal: true,
            MinimumRequirement: new EngineRequirement(
                "Ejecutar un modelo de lenguaje pequeño localmente a través de LM Studio.",
                EngineCostLevel.Moderate, EngineCostLevel.High, EngineCostLevel.Unknown, EngineCostLevel.Moderate),
            RecommendedRequirement: new EngineRequirement(
                "Ejecutar modelos más grandes con mejor calidad de respuesta.",
                EngineCostLevel.High, EngineCostLevel.High, EngineCostLevel.Unknown, EngineCostLevel.High),
            Capabilities: ["Modelos de lenguaje ejecutados localmente a través de LM Studio"],
            Limitations: ["Requiere que LM Studio esté instalado y en ejecución por separado", "El uso de GPU depende de la instalación del usuario"],
            AllowsRuntimeSelection: true,
            RequiresRestart: false,
            RequiresDownload: true,
            IncludedWithSakura: false),

        new EngineDescriptor(
            KnownEngineIds.OpenAiCompatibleLanguageModel,
            "Servidor compatible con OpenAI",
            EngineCategory.LocalLanguageModel,
            IsLocal: null,
            MinimumRequirement: new EngineRequirement(
                "El costo local de Sakura es bajo; el servidor configurado asume el resto.",
                EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Unknown, EngineCostLevel.Low),
            RecommendedRequirement: new EngineRequirement(
                "Igual que el mínimo.",
                EngineCostLevel.Low, EngineCostLevel.Low, EngineCostLevel.Unknown, EngineCostLevel.Low),
            Capabilities: ["Cualquier servidor que implemente la API compatible con OpenAI"],
            Limitations: ["La capacidad real depende completamente del servidor configurado por el usuario"],
            AllowsRuntimeSelection: true,
            RequiresRestart: false,
            RequiresDownload: false,
            IncludedWithSakura: true)
    ];
}
