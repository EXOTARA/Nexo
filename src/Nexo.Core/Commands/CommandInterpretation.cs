namespace Nexo.Core.Commands;

public sealed record CommandInterpretation(
    CommandRoute Route,
    string OriginalText,
    string NormalizedText,
    LocalCommandIntent? Intent = null)
{
    public static CommandInterpretation ForLocal(
        string originalText,
        string normalizedText,
        LocalCommandIntent intent) =>
        new(CommandRoute.Local, originalText, normalizedText, intent);

    public static CommandInterpretation ForAi(
        string originalText,
        string normalizedText) =>
        new(CommandRoute.ArtificialIntelligence, originalText, normalizedText);
}
