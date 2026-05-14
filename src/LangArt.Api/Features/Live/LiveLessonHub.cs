using System.Security.Claims;
using LangArt.Api.Features.Live.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LangArt.Api.Features.Live;

[Authorize]
public class LiveLessonHub : Hub
{
    private readonly SessionRegistry _registry;
    private readonly ILogger<LiveLessonHub> _logger;

    public LiveLessonHub(SessionRegistry registry, ILogger<LiveLessonHub> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// First RPC after the client connects. Resolves the session (lazy-loading from DB on
    /// cold start), checks the caller is authorized, joins the SignalR group, and ships
    /// the full snapshot. Multi-tab fan-in: only the *first* connection per user broadcasts
    /// <c>participant-joined</c>; subsequent tabs are silent to everyone but the caller.
    /// </summary>
    public async Task JoinSession(Guid sessionId)
    {
        var userId = GetUserId();
        var role = GetUserRole();

        var session = await _registry.GetOrLoadAsync(sessionId)
            ?? throw new HubException("Session not found or already ended");

        if (!session.AuthorizedUserIds.Contains(userId))
            throw new HubException("Not authorized for this session");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
        var isFirstConnection = _registry.AddConnection(session, Context.ConnectionId, userId, role);

        var snapshot = new SessionStateSnapshot
        {
            SessionId = session.SessionId,
            LessonId = session.LessonId,
            CurrentBlockIndex = session.CurrentBlockIndex,
            BlockCount = session.BlockCount,
            Participants = session.Participants.Values
                .Select(p => new ParticipantSnapshot
                {
                    UserId = p.UserId,
                    Role = p.Role,
                    Status = p.Status,
                })
                .ToList(),
        };
        await Clients.Caller.SendAsync("session-state", snapshot);

        if (isFirstConnection)
        {
            await Clients.OthersInGroup(GroupName(sessionId))
                .SendAsync("participant-joined", new ParticipantJoinedEvent
                {
                    UserId = userId,
                    Role = role,
                    JoinedAt = DateTime.UtcNow,
                });
        }

        _logger.LogInformation(
            "User {UserId} ({Role}) joined session {SessionId} (connection {ConnectionId}, first={IsFirst})",
            userId, role, sessionId, Context.ConnectionId, isFirstConnection);
    }

    /// <summary>
    /// Teacher (or admin) advances / rewinds the active slide. Every participant in the
    /// session group receives a <c>slide-changed</c> event, including the caller — the
    /// caller's UI uses the echo to confirm the server actually applied the change.
    /// Idempotent: a repeat of the current index is a no-op and emits nothing.
    /// </summary>
    public async Task BroadcastSlide(Guid sessionId, int blockIndex)
    {
        var userId = GetUserId();
        var session = _registry.Get(sessionId)
            ?? throw new HubException("Session not found");

        if (session.TeacherId != userId && GetUserRole() != "admin")
            throw new HubException("Only the session teacher can change slides");

        if (blockIndex < 0 || blockIndex >= session.BlockCount)
            throw new HubException(
                $"Invalid block index: {blockIndex} (must be 0..{session.BlockCount - 1})");

        if (session.CurrentBlockIndex == blockIndex)
            return;

        await _registry.UpdateCurrentBlockAsync(sessionId, blockIndex);

        await Clients.Group(GroupName(sessionId)).SendAsync("slide-changed", new SlideChangedEvent
        {
            BlockIndex = blockIndex,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = userId,
        });

        _logger.LogDebug(
            "Session {SessionId} slide -> {Index} by {UserId}", sessionId, blockIndex, userId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var removed = _registry.RemoveConnection(Context.ConnectionId);
        if (removed is { } r)
        {
            var (info, isLast) = r;
            if (isLast)
            {
                await Clients.Group(GroupName(info.SessionId))
                    .SendAsync("participant-left", new ParticipantLeftEvent
                    {
                        UserId = info.UserId,
                        LeftAt = DateTime.UtcNow,
                    });
            }
            _logger.LogInformation(
                "Connection {ConnectionId} dropped (user {UserId}, session {SessionId}, last={IsLast})",
                info.ConnectionId, info.UserId, info.SessionId, isLast);
        }
        await base.OnDisconnectedAsync(exception);
    }

    private static string GroupName(Guid sessionId) => $"session:{sessionId}";

    private Guid GetUserId()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Context.User?.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var id))
            throw new HubException("Invalid or missing user identity");
        return id;
    }

    private string GetUserRole()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role)
                   ?? Context.User?.FindFirstValue("role");
        if (string.IsNullOrEmpty(role))
            throw new HubException("Invalid or missing role claim");
        return role;
    }
}
