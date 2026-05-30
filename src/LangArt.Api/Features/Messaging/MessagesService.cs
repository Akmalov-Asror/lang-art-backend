using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Messaging.Dto;
using LangArt.Api.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Messaging;

public class MessagesService(
    AppDbContext db,
    ICurrentUser current,
    NotificationsService notifications)
{
    public async Task<List<ConversationSummaryDto>> ListConversationsAsync(CancellationToken ct)
    {
        var meId = current.Id;

        // For each "partner" (other user), pick the latest message and unread count.
        var grouped = await db.Messages
            .Where(m => m.SenderId == meId || m.RecipientId == meId)
            .Select(m => new
            {
                m.Id,
                m.SenderId,
                m.RecipientId,
                m.Body,
                m.CreatedAt,
                m.ReadAt,
                OtherId = m.SenderId == meId ? m.RecipientId : m.SenderId,
            })
            .ToListAsync(ct);

        var partnersByMostRecent = grouped
            .GroupBy(x => x.OtherId)
            .Select(g => new
            {
                OtherId = g.Key,
                Latest = g.OrderByDescending(x => x.CreatedAt).First(),
                UnreadCount = g.Count(x => x.RecipientId == meId && x.ReadAt == null),
            })
            .OrderByDescending(x => x.Latest.CreatedAt)
            .ToList();

        var ids = partnersByMostRecent.Select(p => p.OtherId).ToList();
        var profiles = await db.Profiles
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.FullName, p.Role, p.AvatarUrl })
            .ToListAsync(ct);
        var profById = profiles.ToDictionary(p => p.Id);

        return partnersByMostRecent.Select(p =>
        {
            profById.TryGetValue(p.OtherId, out var prof);
            return new ConversationSummaryDto
            {
                OtherUserId = p.OtherId,
                OtherUserName = prof?.FullName ?? "Unknown",
                OtherUserRole = prof?.Role.ToString().ToLowerInvariant() ?? "",
                AvatarUrl = prof?.AvatarUrl,
                LastMessageBody = p.Latest.Body,
                LastMessageAt = p.Latest.CreatedAt,
                LastMessageSenderId = p.Latest.SenderId,
                UnreadCount = p.UnreadCount,
            };
        }).ToList();
    }

    public async Task<List<MessageDto>> GetThreadAsync(Guid otherUserId, int limit, CancellationToken ct)
    {
        var meId = current.Id;
        if (limit <= 0 || limit > 500) limit = 100;

        var rows = await db.Messages
            .Where(m =>
                (m.SenderId == meId && m.RecipientId == otherUserId) ||
                (m.SenderId == otherUserId && m.RecipientId == meId))
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        rows.Reverse();

        return rows.Select(m => new MessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            RecipientId = m.RecipientId,
            Body = m.Body,
            CreatedAt = m.CreatedAt,
            ReadAt = m.ReadAt,
            IsMine = m.SenderId == meId,
        }).ToList();
    }

    public async Task<MessageDto> SendAsync(SendMessageRequest req, CancellationToken ct)
    {
        var meId = current.Id;
        if (req.RecipientId == meId)
            throw new BadRequestException("Cannot send a message to yourself.");

        var recipient = await db.Profiles
            .Where(p => p.Id == req.RecipientId)
            .Select(p => new { p.Id, p.FullName })
            .FirstOrDefaultAsync(ct);
        if (recipient == null) throw new NotFoundException("Recipient not found.");

        var msg = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = meId,
            RecipientId = req.RecipientId,
            Body = req.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync(ct);

        var senderName = (await db.Profiles
            .Where(p => p.Id == meId)
            .Select(p => p.FullName)
            .FirstOrDefaultAsync(ct)) ?? "Someone";

        // Bell notification + SignalR push (best-effort; NotificationsService dispatches via SignalR after DB insert)
        try
        {
            await notifications.NotifyAsync(
                req.RecipientId,
                "message_received",
                $"New message from {senderName}",
                msg.Body.Length > 120 ? msg.Body[..120] + "…" : msg.Body,
                $"/messages/{meId}");
        }
        catch { /* ignore */ }

        return new MessageDto
        {
            Id = msg.Id,
            SenderId = msg.SenderId,
            RecipientId = msg.RecipientId,
            Body = msg.Body,
            CreatedAt = msg.CreatedAt,
            ReadAt = msg.ReadAt,
            IsMine = true,
        };
    }

    public async Task MarkThreadReadAsync(Guid otherUserId, CancellationToken ct)
    {
        var meId = current.Id;
        await db.Messages
            .Where(m => m.RecipientId == meId && m.SenderId == otherUserId && m.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadAt, DateTime.UtcNow), ct);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct)
    {
        var meId = current.Id;
        return await db.Messages.CountAsync(m => m.RecipientId == meId && m.ReadAt == null, ct);
    }

    public async Task<List<ContactDto>> ListContactsAsync(string? search, CancellationToken ct)
    {
        var meId = current.Id;
        var q = db.Profiles.Where(p => p.Id != meId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(p => p.FullName.ToLower().Contains(s) || (p.Email != null && p.Email.ToLower().Contains(s)));
        }

        return await q
            .OrderBy(p => p.FullName)
            .Take(50)
            .Select(p => new ContactDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Role = p.Role.ToString().ToLowerInvariant(),
                AvatarUrl = p.AvatarUrl,
                Email = p.Email,
            })
            .ToListAsync(ct);
    }
}
