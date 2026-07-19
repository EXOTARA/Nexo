namespace Nexo.Core.Focus;

public sealed record FocusCommand(
    FocusCommandType Type,
    string RawText,
    TimeSpan? Duration = null,
    string Label = "",
    FocusSessionKind Kind = FocusSessionKind.Custom)
{
    public static FocusCommand None(string rawText) =>
        new(FocusCommandType.None, rawText);
}
