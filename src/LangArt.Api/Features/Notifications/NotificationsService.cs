using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Notifications.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Notifications;

public class NotificationsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationsService> _logger;
    private readonly Realtime.INotificationDispatcher _dispatcher;

    public NotificationsService(
        AppDbContext db,
        ILogger<NotificationsService> logger,
        Realtime.INotificationDispatcher dispatcher)
    {
        _db = db;
        _logger = logger;
        _dispatcher = dispatcher;
    }

    public async Task<List<NotificationResponse>> ListAsync(Guid userId, bool unreadOnly)
    {
        var q = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) q = q.Where(n => n.ReadAt == null);
        var rows = await q.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync();
        return rows.Select(ToResponse).ToList();
    }

    public Task<int> UnreadCountAsync(Guid userId) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null);

    public async Task<int> MarkAllReadAsync(Guid userId)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && n.Id == notificationId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    /// <summary>
    /// Called by other services when they want to drop a notification in someone's inbox.
    /// Fire-and-forget from the caller's perspective — exceptions are swallowed.
    /// </summary>
    public async Task NotifyAsync(Guid userId, string kind, string title, string? body = null, string? linkUrl = null)
    {
        Notification? row = null;
        try
        {
            row = new Notification
            {
                UserId = userId,
                Kind = kind,
                Title = title,
                Body = body,
                LinkUrl = linkUrl,
            };
            _db.Notifications.Add(row);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Notifications are best-effort — never break the caller, but log loudly so
            // misconfigured schema / FK issues surface during development.
            _logger.LogWarning(ex, "Failed to insert notification kind={Kind} for user={UserId}", kind, userId);
            return;
        }

        // Sprint 2: also fan-out via SignalR for any tabs currently connected. Polling
        // path covers offline users + cold-start on the next refresh. Dispatcher swallows
        // its own errors so this call cannot break the originating transaction.
        await _dispatcher.SendNotificationAsync(userId, new Realtime.Dto.NotificationDto
        {
            Id = row.Id,
            UserId = row.UserId,
            Type = row.Kind,            // wire field name is `type` per Sprint 2 contract
            Title = row.Title,
            Body = row.Body,
            LinkUrl = row.LinkUrl,
            CreatedAtUtc = row.CreatedAt,
        });
    }

    private static NotificationResponse ToResponse(Notification n) => new()
    {
        Id = n.Id,
        Kind = n.Kind,
        Title = n.Title,
        Body = n.Body,
        LinkUrl = n.LinkUrl,
        ReadAt = n.ReadAt,
        CreatedAt = n.CreatedAt,
    };
}
