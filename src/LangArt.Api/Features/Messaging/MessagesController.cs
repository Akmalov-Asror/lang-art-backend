using LangArt.Api.Features.Messaging.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Messaging;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController(MessagesService service) : ControllerBase
{
    [HttpGet("conversations")]
    public async Task<ActionResult<List<ConversationSummaryDto>>> ListConversations(CancellationToken ct)
        => Ok(await service.ListConversationsAsync(ct));

    [HttpGet("thread/{otherUserId:guid}")]
    public async Task<ActionResult<List<MessageDto>>> GetThread(Guid otherUserId, [FromQuery] int limit = 100, CancellationToken ct = default)
        => Ok(await service.GetThreadAsync(otherUserId, limit, ct));

    [HttpPost]
    public async Task<ActionResult<MessageDto>> Send([FromBody] SendMessageRequest req, CancellationToken ct)
        => Ok(await service.SendAsync(req, ct));

    [HttpPost("thread/{otherUserId:guid}/read")]
    public async Task<ActionResult> MarkRead(Guid otherUserId, CancellationToken ct)
    {
        await service.MarkThreadReadAsync(otherUserId, ct);
        return Ok(new { success = true });
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> UnreadCount(CancellationToken ct)
        => Ok(new { count = await service.GetUnreadCountAsync(ct) });

    [HttpGet("contacts")]
    public async Task<ActionResult<List<ContactDto>>> ListContacts([FromQuery] string? search, CancellationToken ct)
        => Ok(await service.ListContactsAsync(search, ct));
}
