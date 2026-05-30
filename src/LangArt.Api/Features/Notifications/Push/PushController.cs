using LangArt.Api.Common.Auth;
using LangArt.Api.Data;
using LangArt.Api.Features.Notifications.Push.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Notifications.Push;

[ApiController]
[Route("api/notifications/push")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public PushController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest dto, CancellationToken ct)
    {
        // Upsert on endpoint. Browser may renew the same endpoint string for the
        // same device; we want one row per device per user.
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint, ct);

        if (existing is null)
        {
            _db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = _currentUser.Id,
                Endpoint = dto.Endpoint,
                P256dh = dto.Keys.P256dh,
                Auth = dto.Keys.Auth,
                UserAgent = dto.UserAgent,
            });
        }
        else
        {
            existing.UserId = _currentUser.Id;
            existing.P256dh = dto.Keys.P256dh;
            existing.Auth = dto.Keys.Auth;
            existing.UserAgent = dto.UserAgent;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { });
    }

    [HttpDelete("subscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest dto, CancellationToken ct)
    {
        await _db.PushSubscriptions
            .Where(s => s.Endpoint == dto.Endpoint && s.UserId == _currentUser.Id)
            .ExecuteDeleteAsync(ct);
        return Ok(new { });
    }
}
