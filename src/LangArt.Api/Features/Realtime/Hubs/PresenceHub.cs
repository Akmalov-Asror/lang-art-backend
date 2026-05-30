using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LangArt.Api.Features.Realtime.Hubs;

/// <summary>
/// Separate hub for "is X online?" dashboard widgets. Currently a thin tracker
/// — interested observers (teachers viewing classroom rosters) read presence
/// via the REST endpoint <c>GET /api/realtime/presence/classroom/{groupId}</c>
/// for cold start, then subscribe here for live UserOnline / UserOffline events.
///
/// In Sprint 2 this hub does not yet broadcast on connect/disconnect — that
/// requires deciding which SignalR groups to broadcast to (per-classroom?
/// per-teacher's-students?) and is wired in Sprint 3 when the teacher
/// dashboard adds the live roster. The hub is mapped now so the frontend has
/// a stable URL to point at.
/// </summary>
[Authorize]
public class PresenceHub : Hub<IPresenceClient>
{
    private readonly IConnectionTracker _tracker;
    private readonly ILogger<PresenceHub> _logger;

    public PresenceHub(IConnectionTracker tracker, ILogger<PresenceHub> logger)
    {
        _tracker = tracker;
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            _tracker.AddConnection(userId, Context.ConnectionId);
        }
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _tracker.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Optional client→server heartbeat so the SignalR transport's own keep-alive
    /// + a lightweight DB-touch can be split apart later. Today this is a no-op
    /// — the SignalR ping/pong already keeps the connection alive. Reserved for
    /// future "last seen at" persistence.
    /// </summary>
    public Task HeartbeatAsync() => Task.CompletedTask;
}
