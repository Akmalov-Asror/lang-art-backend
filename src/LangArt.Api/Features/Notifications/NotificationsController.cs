using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Notifications.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Notifications;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationsService _svc;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(NotificationsService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet("")]
    public Task<List<NotificationResponse>> List([FromQuery] bool unread = false) =>
        _svc.ListAsync(_currentUser.Id, unread);

    [HttpGet("unread-count")]
    public async Task<UnreadCountResponse> UnreadCount() =>
        new UnreadCountResponse { Count = await _svc.UnreadCountAsync(_currentUser.Id) };

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _svc.MarkAllReadAsync(_currentUser.Id);
        return Ok(new { });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _svc.MarkReadAsync(_currentUser.Id, id);
        return Ok(new { });
    }
}
