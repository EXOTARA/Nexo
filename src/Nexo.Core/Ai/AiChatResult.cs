namespace Nexo.Core.Ai;

public sealed record AiChatResult(
    bool IsSuccess,
    string Text,
    string Detail)
{
    public static AiChatResult Success(string text) =>
        new(true, text.Trim(), "Respuesta recibida.");

    public static AiChatResult Failed(string detail) =>
        new(false, string.Empty, detail);
}
