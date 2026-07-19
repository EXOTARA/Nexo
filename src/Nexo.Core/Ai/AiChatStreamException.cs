namespace Nexo.Core.Ai;

public sealed class AiChatStreamException : Exception
{
    public AiChatStreamException(string message)
        : base(message)
    {
    }

    public AiChatStreamException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
