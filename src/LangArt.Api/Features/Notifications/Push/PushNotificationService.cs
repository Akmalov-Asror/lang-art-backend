using System.Text.Json;
using LangArt.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;

namespace LangArt.Api.Features.Notifications.Push;

public class PushNotificationService : IPushNotificationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly VapidDetails? _vapid;

    public PushNotificationService(AppDbContext db, ILogger<PushNotificationService> logger, IOptions<PushOptions> opts)
    {
        _db = db;
        _logger = logger;
        var o = opts.Value;
        if (!string.IsNullOrWhiteSpace(o.VapidPublicKey) && !string.IsNullOrWhiteSpace(o.VapidPrivateKey))
        {
            _vapid = new VapidDetails(o.Subject, o.VapidPublicKey, o.VapidPrivateKey);
        }
        else
        {
            _logger.LogWarning(
                "VAPID keys are not configured. Push notifications will be a no-op until you set " +
                "Notifications:Push:VapidPublicKey / VapidPrivateKey (generate with `npx web-push generate-vapid-keys`).");
        }
    }

    public async Task SendToUserAsync(Guid userId, PushPayload payload, CancellationToken ct = default)
    {
        if (_vapid is null) return;

        var subs = await _db.PushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);
        if (subs.Count == 0) return;

        var json = JsonSerializer.Serialize(new
        {
            title = payload.Title,
            body = payload.Body,
            tag = payload.Tag,
            url = payload.Url,
        });

        var client = new WebPushClient();
        var endpointsToRemove = new List<Guid>();

        foreach (var sub in subs)
        {
            try
            {
                var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, json, _vapid);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound
                                              || ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // Subscription is dead — schedule removal.
                endpointsToRemove.Add(sub.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push send failed for endpoint {Endpoint}", sub.Endpoint);
            }
        }

        if (endpointsToRemove.Count > 0)
        {
            await _db.PushSubscriptions
                .Where(s => endpointsToRemove.Contains(s.Id))
                .ExecuteDeleteAsync(ct);
            _logger.LogInformation("Removed {Count} dead push subscription(s)", endpointsToRemove.Count);
        }
    }
}
