using Nexo.Core.Assistant;

namespace Nexo.Core.Ai;

public sealed record AiChatRequest(
    IReadOnlyList<ConversationMessage> Messages,
    string Instructions,
    string? SystemContext = null,
    IReadOnlyList<AiImageAttachment>? Images = null,
    AiRequestMode Mode = AiRequestMode.Standard);
