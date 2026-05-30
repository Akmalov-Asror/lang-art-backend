using LangArt.Api.Features.AiChat.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.AiChat;

[ApiController]
[Route("api/ai-chat")]
[Authorize]
public class AiChatController : ControllerBase
{
    private readonly IAiChatService _svc;

    public AiChatController(IAiChatService svc)
    {
        _svc = svc;
    }

    [HttpPost("ask")]
    public Task<ChatResponse> Ask([FromBody] ChatRequest req, CancellationToken ct) =>
        _svc.ReplyAsync(req, ct);
}
