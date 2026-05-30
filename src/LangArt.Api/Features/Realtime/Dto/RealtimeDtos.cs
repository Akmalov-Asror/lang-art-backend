namespace LangArt.Api.Features.Realtime.Dto;

/// <summary>
/// Hub payload sent on <c>INotificationsClient.NotificationReceived</c>.
/// Property names go on the wire as snake_case (configured globally on the
/// SignalR JSON protocol in Program.cs), so they line up exactly with the
/// REST <c>GET /api/notifications</c> response shape. Shape matches the
/// reference contract in the Sprint 2 brief.
/// </summary>
public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "system";
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public IDictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Hub payload for <c>INotificationsClient.BadgeEarned</c>. Used for online
/// users in addition to (not instead of) the Web Push notification from
/// Sprint 1, which still reaches offline users.
/// </summary>
public class BadgeEarnedDto
{
    public BadgePayload Badge { get; set; } = new();
    public DateTime EarnedAtUtc { get; set; }
}

public class BadgePayload
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public int XpReward { get; set; }
}

public class ClassroomEventDto
{
    public string Type { get; set; } = string.Empty;   // e.g. "user_joined", "user_left", "block_advanced"
    public Guid? ActorUserId { get; set; }
    public DateTime AtUtc { get; set; }
    public IDictionary<string, object>? Data { get; set; }
}

public class ClassroomPresenceResponse
{
    public Guid GroupId { get; set; }
    public IReadOnlyList<Guid> OnlineUserIds { get; set; } = Array.Empty<Guid>();
}
