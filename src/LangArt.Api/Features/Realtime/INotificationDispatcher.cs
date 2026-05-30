using LangArt.Api.Features.Realtime.Dto;

namespace LangArt.Api.Features.Realtime;

/// <summary>
/// Fan-out for hub events triggered by other services (e.g. <c>NotificationsService</c>,
/// <c>GamificationService</c>). Wraps the strongly-typed <c>IHubContext</c> so callers
/// don't see SignalR's API directly and so call sites read like a domain action
/// ("dispatch a notification to this user") rather than a transport detail.
///
/// Semantics: all methods are <b>best-effort</b>. If the recipient isn't online,
/// <c>Clients.User(...)</c> is a no-op — the polling endpoint + Web Push from
/// Sprint 1 cover the offline delivery paths.
/// </summary>
public interface INotificationDispatcher
{
    Task SendNotificationAsync(Guid userId, NotificationDto payload, CancellationToken ct = default);
    Task SendNotificationsAsync(IEnumerable<Guid> userIds, NotificationDto payload, CancellationToken ct = default);
    Task SendBadgeEarnedAsync(Guid userId, BadgeEarnedDto payload, CancellationToken ct = default);

    /// <summary>Broadcast a classroom event to everyone in the SignalR group <c>classroom:{groupId}</c>.</summary>
    Task SendClassroomEventAsync(Guid groupId, ClassroomEventDto payload, CancellationToken ct = default);
}
