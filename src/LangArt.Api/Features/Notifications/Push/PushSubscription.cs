namespace LangArt.Api.Features.Notifications.Push;

/// <summary>
/// One row per browser/device a user has opted into web push from. Browsers
/// reuse the same <c>Endpoint</c> for renewals, so we unique-index it and
/// upsert on subscribe.
/// </summary>
public class PushSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Data.Entities.Profile User { get; set; } = null!;
}
