using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LangArt.Api.Features.Realtime.Hubs;

/// <summary>
/// Per-user bell-icon notifications + badge-earned events. Authorisation is required
/// — the global <c>FallbackPolicy = RequireAuthenticatedUser()</c> applies to MVC
/// endpoints only, so we annotate the hub explicitly. SignalR's JWT lives in the
/// <c>access_token</c> query string (see <c>Program.cs</c> <c>OnMessageReceived</c>).
///
/// Also hosts the client→server join/leave classroom calls. Putting them here rather
/// than on a separate <c>ClassroomHub</c> means a student needs only one connection
/// per tab — the connection-count constraint matters more than per-feature isolation
/// at this scale. (Sprint 3 may split into a dedicated <c>ClassroomHub</c> if
/// presence/bell traffic patterns diverge.)
/// </summary>
[Authorize]
public class NotificationsHub : Hub<INotificationsClient>
{
    private readonly IConnectionTracker _tracker;
    private readonly IClassroomConnectionService _classroom;
    private readonly ILogger<NotificationsHub> _logger;

    public NotificationsHub(
        IConnectionTracker tracker,
        IClassroomConnectionService classroom,
        ILogger<NotificationsHub> logger)
    {
        _tracker = tracker;
        _classroom = classroom;
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        if (TryGetUserId(out var userId))
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

    // ---------------- Classroom (Phase C of Sprint 2) ----------------

    /// <summary>
    /// Joins the requesting connection to the classroom group's SignalR group, after
    /// verifying membership / teacher access in <see cref="IClassroomConnectionService.JoinAsync"/>.
    /// Returns <c>true</c> on success, <c>false</c> if authorisation fails — the
    /// frontend can surface that as a UI banner.
    /// </summary>
    public async Task<bool> JoinClassroomAsync(Guid groupId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return false;
        var result = await _classroom.JoinAsync(userId, groupId, Context.ConnectionId, ct);
        return result.Allowed;
    }

    public async Task LeaveClassroomAsync(Guid groupId, CancellationToken ct)
    {
        await _classroom.LeaveAsync(Context.ConnectionId, groupId, ct);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = Context.UserIdentifier;
        if (Guid.TryParse(raw, out userId)) return true;
        _logger.LogWarning("Hub connection has no parseable user id (raw={Raw})", raw);
        return false;
    }
}
