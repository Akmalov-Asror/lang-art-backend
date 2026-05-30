using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Clubs.Dto;
using LangArt.Api.Features.LaDollar;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Clubs;

public class ClubsService(AppDbContext db, ICurrentUser current, ILaDollarService laDollar)
{
    private static readonly HashSet<string> ValidKinds = new() { "club", "workshop", "debate", "visit", "social" };
    private static readonly HashSet<string> RsvpStatuses = new() { "going", "maybe", "attended", "no_show" };

    public async Task<List<ClubEventDto>> ListAsync(string? kind, bool upcomingOnly, CancellationToken ct)
    {
        var q = db.ClubEvents.AsQueryable();
        if (!string.IsNullOrWhiteSpace(kind)) q = q.Where(e => e.Kind == kind);
        if (upcomingOnly) q = q.Where(e => e.StartsAt >= DateTime.UtcNow.AddHours(-2));

        var events = await q.OrderBy(e => e.StartsAt).Take(200).ToListAsync(ct);
        if (events.Count == 0) return new();

        return await EnrichAsync(events, ct);
    }

    public async Task<ClubEventDto> GetAsync(Guid id, CancellationToken ct)
    {
        var e = await db.ClubEvents.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Event not found.");
        var dtos = await EnrichAsync(new List<ClubEvent> { e }, ct);
        return dtos[0];
    }

    public async Task<List<ClubEventRsvpDto>> ListRsvpsAsync(Guid eventId, CancellationToken ct)
    {
        var exists = await db.ClubEvents.AnyAsync(e => e.Id == eventId, ct);
        if (!exists) throw new NotFoundException("Event not found.");

        var rows = await db.ClubEventRsvps
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var ids = rows.Select(r => r.UserId).Distinct().ToList();
        var names = await db.Profiles
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.FullName })
            .ToDictionaryAsync(p => p.Id, p => p.FullName, ct);

        return rows.Select(r => new ClubEventRsvpDto
        {
            Id = r.Id,
            EventId = r.EventId,
            UserId = r.UserId,
            UserName = names.GetValueOrDefault(r.UserId, "Unknown"),
            Status = r.Status,
            CreatedAt = r.CreatedAt,
        }).ToList();
    }

    public async Task<ClubEventDto> CreateAsync(CreateEventRequest req, CancellationToken ct)
    {
        if (!ValidKinds.Contains(req.Kind))
            throw new BadRequestException($"Invalid kind. Valid: {string.Join(",", ValidKinds)}");

        var e = new ClubEvent
        {
            Id = Guid.NewGuid(),
            Title = req.Title.Trim(),
            Kind = req.Kind,
            Description = req.Description,
            Location = req.Location,
            StartsAt = req.StartsAt,
            EndsAt = req.EndsAt,
            Capacity = req.Capacity,
            LaDollarReward = Math.Max(0, req.LaDollarReward),
            CreatedBy = current.Id,
            BranchId = req.BranchId,
            CoverImageUrl = req.CoverImageUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ClubEvents.Add(e);
        await db.SaveChangesAsync(ct);
        return await GetAsync(e.Id, ct);
    }

    public async Task<ClubEventDto> UpdateAsync(Guid id, UpdateEventRequest req, CancellationToken ct)
    {
        var e = await db.ClubEvents.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Event not found.");

        if (req.Title != null) e.Title = req.Title.Trim();
        if (req.Kind != null)
        {
            if (!ValidKinds.Contains(req.Kind)) throw new BadRequestException("Invalid kind.");
            e.Kind = req.Kind;
        }
        if (req.Description != null) e.Description = req.Description;
        if (req.Location != null) e.Location = req.Location;
        if (req.StartsAt.HasValue) e.StartsAt = req.StartsAt.Value;
        if (req.EndsAt.HasValue) e.EndsAt = req.EndsAt;
        if (req.Capacity.HasValue) e.Capacity = req.Capacity;
        if (req.LaDollarReward.HasValue) e.LaDollarReward = Math.Max(0, req.LaDollarReward.Value);
        if (req.BranchId.HasValue) e.BranchId = req.BranchId;
        if (req.IsCancelled.HasValue) e.IsCancelled = req.IsCancelled.Value;
        if (req.CoverImageUrl != null) e.CoverImageUrl = req.CoverImageUrl;
        e.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var rows = await db.ClubEvents.Where(e => e.Id == id).ExecuteDeleteAsync(ct);
        if (rows == 0) throw new NotFoundException("Event not found.");
    }

    public async Task<ClubEventRsvpDto> RsvpAsync(Guid eventId, RsvpRequest req, CancellationToken ct)
    {
        if (req.Status != "going" && req.Status != "maybe")
            throw new BadRequestException("Status must be 'going' or 'maybe'.");

        var e = await db.ClubEvents.FirstOrDefaultAsync(x => x.Id == eventId, ct)
            ?? throw new NotFoundException("Event not found.");
        if (e.IsCancelled) throw new BadRequestException("Event is cancelled.");

        if (e.Capacity.HasValue)
        {
            var going = await db.ClubEventRsvps
                .CountAsync(r => r.EventId == eventId && (r.Status == "going" || r.Status == "attended"), ct);
            var existing = await db.ClubEventRsvps
                .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == current.Id, ct);
            // Only block if user is newly going and capacity reached
            if (req.Status == "going" && (existing == null || existing.Status != "going") && going >= e.Capacity.Value)
                throw new ConflictException("Event is full.");
        }

        var rsvp = await db.ClubEventRsvps.FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == current.Id, ct);
        if (rsvp == null)
        {
            rsvp = new ClubEventRsvp
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                UserId = current.Id,
                Status = req.Status,
                CreatedAt = DateTime.UtcNow,
            };
            db.ClubEventRsvps.Add(rsvp);
        }
        else
        {
            rsvp.Status = req.Status;
        }
        await db.SaveChangesAsync(ct);

        var name = await db.Profiles.Where(p => p.Id == current.Id).Select(p => p.FullName).FirstOrDefaultAsync(ct) ?? "You";
        return new ClubEventRsvpDto
        {
            Id = rsvp.Id,
            EventId = rsvp.EventId,
            UserId = rsvp.UserId,
            UserName = name,
            Status = rsvp.Status,
            CreatedAt = rsvp.CreatedAt,
        };
    }

    public async Task CancelRsvpAsync(Guid eventId, CancellationToken ct)
    {
        await db.ClubEventRsvps
            .Where(r => r.EventId == eventId && r.UserId == current.Id && (r.Status == "going" || r.Status == "maybe"))
            .ExecuteDeleteAsync(ct);
    }

    public async Task MarkAttendanceAsync(Guid eventId, MarkAttendanceRequest req, CancellationToken ct)
    {
        var e = await db.ClubEvents.FirstOrDefaultAsync(x => x.Id == eventId, ct)
            ?? throw new NotFoundException("Event not found.");

        foreach (var (userId, status) in req.Statuses)
        {
            if (!RsvpStatuses.Contains(status)) continue;
            var rsvp = await db.ClubEventRsvps.FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId, ct);
            if (rsvp == null) continue;
            var wasAttended = rsvp.Status == "attended";
            rsvp.Status = status;
            await db.SaveChangesAsync(ct);

            // Award LA Dollar only on first transition to attended, and only if reward > 0
            if (!wasAttended && status == "attended" && e.LaDollarReward > 0)
            {
                await laDollar.AwardAsync(
                    userId,
                    "admin_adjustment",      // free-text reason column; idempotent on (user, reason, source)
                    e.LaDollarReward,
                    e.Id,
                    $"Attended: {e.Title}",
                    ct);
            }
        }
    }

    private async Task<List<ClubEventDto>> EnrichAsync(List<ClubEvent> events, CancellationToken ct)
    {
        var ids = events.Select(e => e.Id).ToList();
        var creatorIds = events.Select(e => e.CreatedBy).Distinct().ToList();

        var creators = await db.Profiles
            .Where(p => creatorIds.Contains(p.Id))
            .Select(p => new { p.Id, p.FullName })
            .ToDictionaryAsync(p => p.Id, p => p.FullName, ct);

        var rsvpStats = await db.ClubEventRsvps
            .Where(r => ids.Contains(r.EventId))
            .GroupBy(r => new { r.EventId, r.Status })
            .Select(g => new { g.Key.EventId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var myRsvps = await db.ClubEventRsvps
            .Where(r => ids.Contains(r.EventId) && r.UserId == current.Id)
            .Select(r => new { r.EventId, r.Status })
            .ToDictionaryAsync(r => r.EventId, r => r.Status, ct);

        return events.Select(e =>
        {
            var stats = rsvpStats.Where(s => s.EventId == e.Id).ToList();
            var rsvpCount = stats.Where(s => s.Status == "going" || s.Status == "attended" || s.Status == "maybe").Sum(s => s.Count);
            var attendedCount = stats.Where(s => s.Status == "attended").Sum(s => s.Count);
            return new ClubEventDto
            {
                Id = e.Id,
                Title = e.Title,
                Kind = e.Kind,
                Description = e.Description,
                Location = e.Location,
                StartsAt = e.StartsAt,
                EndsAt = e.EndsAt,
                Capacity = e.Capacity,
                LaDollarReward = e.LaDollarReward,
                CreatedBy = e.CreatedBy,
                CreatedByName = creators.GetValueOrDefault(e.CreatedBy, "Unknown"),
                BranchId = e.BranchId,
                IsCancelled = e.IsCancelled,
                CoverImageUrl = e.CoverImageUrl,
                CreatedAt = e.CreatedAt,
                RsvpCount = rsvpCount,
                AttendedCount = attendedCount,
                MyRsvpStatus = myRsvps.GetValueOrDefault(e.Id),
            };
        }).ToList();
    }
}
