using LangArt.Api.Features.Realtime.Dto;
using LangArt.Api.Features.Realtime.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace LangArt.Api.Features.Realtime;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IHubContext<NotificationsHub, INotificationsClient> _hub;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IHubContext<NotificationsHub, INotificationsClient> hub,
        ILogger<NotificationDispatcher> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task SendNotificationAsync(Guid userId, NotificationDto payload, CancellationToken ct = default)
    {
        try
        {
            // Clients.User uses the IUserIdProvider — which we've bound to the JWT `sub`
            // claim. The userId argument MUST be the same string format SignalR sees.
            await _hub.Clients.User(userId.ToString()).NotificationReceived(payload);
        }
        catch (Exception ex)
        {
            // Best-effort: never block the originating service on a dispatch failure.
            _logger.LogWarning(ex, "Hub dispatch (NotificationReceived) failed for {UserId}", userId);
        }
    }

    public async Task SendNotificationsAsync(IEnumerable<Guid> userIds, NotificationDto payload, CancellationToken ct = default)
    {
        try
        {
            var ids = userIds.Select(u => u.ToString()).ToList();
            if (ids.Count == 0) return;
            await _hub.Clients.Users(ids).NotificationReceived(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hub dispatch (fan-out NotificationReceived) failed for {Count} users", userIds.Count());
        }
    }

    public async Task SendBadgeEarnedAsync(Guid userId, BadgeEarnedDto payload, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.User(userId.ToString()).BadgeEarned(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hub dispatch (BadgeEarned) failed for {UserId}", userId);
        }
    }

    public async Task SendClassroomEventAsync(Guid groupId, ClassroomEventDto payload, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group($"classroom:{groupId}").ClassroomEvent(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hub dispatch (ClassroomEvent) failed for {GroupId}", groupId);
        }
    }
}
