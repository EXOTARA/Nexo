using Nexo.Core.Ai;
using Nexo.Core.Settings;

namespace Nexo.Core.AdaptiveEngine;

public interface IAdaptiveEngineRegistry
{
    IReadOnlyList<EngineDescriptor> GetDescriptors();

    IReadOnlyList<EngineRuntimeState> CaptureRuntimeStates(
        ShellPreferences preferences,
        OllamaRuntimeSnapshot? ollamaRuntimeSnapshot);
}
