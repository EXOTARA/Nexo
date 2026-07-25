using Nexo.Core.AdaptiveEngine;

namespace Nexo.Windows.AdaptiveEngine;

internal static class KnownEngineIds
{
    public static readonly EngineIdentifier WhisperSpeechToText = new("kohana.stt.whisper");
    public static readonly EngineIdentifier VoskWakeWord = new("kohana.wakeword.vosk");
    public static readonly EngineIdentifier WindowsSapiTextToSpeech = new("kohana.tts.sapi");
    public static readonly EngineIdentifier OpenAiLanguageModel = new("kohana.llm.openai");
    public static readonly EngineIdentifier OllamaLanguageModel = new("kohana.llm.ollama");
    public static readonly EngineIdentifier LmStudioLanguageModel = new("kohana.llm.lmstudio");
    public static readonly EngineIdentifier OpenAiCompatibleLanguageModel = new("kohana.llm.openai-compatible");
}
