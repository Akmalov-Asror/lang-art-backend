using LangArt.Api.Features.AiChat.Dto;

namespace LangArt.Api.Features.AiChat;

public class StubAiChatService : IAiChatService
{
    public Task<ChatResponse> ReplyAsync(ChatRequest req, CancellationToken ct)
    {
        var reply = $"(Demo reply — no OpenAI key configured.)\n\n" +
                    $"You asked: \"{req.Message}\"\n\n" +
                    $"In a real deployment, an English tutor (GPT-4o-mini) would explain this " +
                    $"using the lesson context, with examples and grammar rules.";
        return Task.FromResult(new ChatResponse { Reply = reply, Provider = "stub" });
    }
}
