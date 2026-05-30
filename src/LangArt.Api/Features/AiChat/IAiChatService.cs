using LangArt.Api.Features.AiChat.Dto;

namespace LangArt.Api.Features.AiChat;

/// <summary>
/// Conversational tutor service for the "Ask from LA" feature. Students chat
/// in-lesson about grammar, vocabulary, translations. Real impl uses GPT-4o-mini;
/// stub returns a friendly canned reply when no API key is configured.
/// </summary>
public interface IAiChatService
{
    Task<ChatResponse> ReplyAsync(ChatRequest req, CancellationToken ct);
}
