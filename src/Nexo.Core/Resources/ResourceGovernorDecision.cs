namespace Nexo.Core.Resources;

public sealed record ResourceGovernorDecision(
    ResourceMode Mode,
    string Reason,
    bool AllowLocalCommands,
    bool AllowLocalAi,
    bool AllowRemoteAi,
    bool AllowVision,
    bool PauseWakeWord,
    bool SuppressTransientOverlays)
{
    public static ResourceGovernorDecision Normal { get; } = new(
        ResourceMode.Normal,
        "Recursos disponibles.",
        AllowLocalCommands: true,
        AllowLocalAi: true,
        AllowRemoteAi: true,
        AllowVision: true,
        PauseWakeWord: false,
        SuppressTransientOverlays: false);

    public bool IsDegraded => Mode != ResourceMode.Normal;
}
