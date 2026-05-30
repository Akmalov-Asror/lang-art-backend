namespace LangArt.Api.Features.Notifications.Push;

public class PushPayload
{
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? Tag { get; set; }
    public string? Url { get; set; }
}

public class PushOptions
{
    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;
    public string Subject { get; set; } = "mailto:no-reply@langartlms.com";
}

public interface IPushNotificationService
{
    /// <summary>
    /// Sends the payload to every active subscription on file for <paramref name="userId"/>.
    /// Dead endpoints (push service returns 404/410 — the user revoked or the
    /// browser GC'd the subscription) are silently removed. Other failures are
    /// logged but not re-thrown, so callers can fire-and-forget.
    /// </summary>
    Task SendToUserAsync(Guid userId, PushPayload payload, CancellationToken ct = default);
}
