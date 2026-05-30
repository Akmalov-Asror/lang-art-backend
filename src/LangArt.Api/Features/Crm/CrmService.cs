using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Crm.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Crm;

public class CrmService(AppDbContext db, ICurrentUser current)
{
    private static readonly HashSet<string> ValidStages = new()
    {
        "new", "contacted", "trial_scheduled", "converted", "lost",
    };

    public async Task<List<LeadDto>> ListAsync(string? stage, Guid? assignedTo, CancellationToken ct)
    {
        var q = db.Leads.AsQueryable();
        if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(l => l.Stage == stage);
        if (assignedTo.HasValue) q = q.Where(l => l.AssignedTo == assignedTo);

        var leads = await q.OrderByDescending(l => l.UpdatedAt).Take(500).ToListAsync(ct);
        if (leads.Count == 0) return new();

        var assigneeIds = leads.Where(l => l.AssignedTo.HasValue).Select(l => l.AssignedTo!.Value).Distinct().ToList();
        var names = await db.Profiles
            .Where(p => assigneeIds.Contains(p.Id))
            .Select(p => new { p.Id, p.FullName })
            .ToDictionaryAsync(p => p.Id, p => p.FullName, ct);

        var leadIds = leads.Select(l => l.Id).ToList();
        var counts = await db.LeadActivities
            .Where(a => leadIds.Contains(a.LeadId))
            .GroupBy(a => a.LeadId)
            .Select(g => new { LeadId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LeadId, x => x.Count, ct);

        return leads.Select(l => ToDto(l, names, counts)).ToList();
    }

    public async Task<LeadDto> GetAsync(Guid id, CancellationToken ct)
    {
        var l = await db.Leads.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Lead not found.");
        var names = new Dictionary<Guid, string>();
        if (l.AssignedTo.HasValue)
        {
            var n = await db.Profiles.Where(p => p.Id == l.AssignedTo).Select(p => p.FullName).FirstOrDefaultAsync(ct);
            if (n != null) names[l.AssignedTo.Value] = n;
        }
        var count = await db.LeadActivities.CountAsync(a => a.LeadId == id, ct);
        return ToDto(l, names, new Dictionary<Guid, int> { [id] = count });
    }

    public async Task<List<LeadActivityDto>> ListActivitiesAsync(Guid leadId, CancellationToken ct)
    {
        var exists = await db.Leads.AnyAsync(l => l.Id == leadId, ct);
        if (!exists) throw new NotFoundException("Lead not found.");

        var rows = await db.LeadActivities
            .Where(a => a.LeadId == leadId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var actorIds = rows.Select(a => a.ActorId).Distinct().ToList();
        var actors = await db.Profiles
            .Where(p => actorIds.Contains(p.Id))
            .Select(p => new { p.Id, p.FullName })
            .ToDictionaryAsync(p => p.Id, p => p.FullName, ct);

        return rows.Select(a => new LeadActivityDto
        {
            Id = a.Id,
            LeadId = a.LeadId,
            ActorId = a.ActorId,
            ActorName = actors.GetValueOrDefault(a.ActorId, "Unknown"),
            Kind = a.Kind,
            Body = a.Body,
            CreatedAt = a.CreatedAt,
        }).ToList();
    }

    public async Task<LeadDto> CreateAsync(CreateLeadRequest req, CancellationToken ct)
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            FullName = req.FullName.Trim(),
            Phone = req.Phone?.Trim(),
            Email = req.Email?.Trim().ToLowerInvariant(),
            Source = string.IsNullOrWhiteSpace(req.Source) ? "other" : req.Source,
            Stage = "new",
            InterestLevel = req.InterestLevel,
            Notes = req.Notes,
            AssignedTo = req.AssignedTo,
            NextFollowUpAt = req.NextFollowUpAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Leads.Add(lead);

        db.LeadActivities.Add(new LeadActivity
        {
            Id = Guid.NewGuid(),
            LeadId = lead.Id,
            ActorId = current.Id,
            Kind = "note",
            Body = $"Lead created — source: {lead.Source}",
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        return await GetAsync(lead.Id, ct);
    }

    public async Task<LeadDto> UpdateAsync(Guid id, UpdateLeadRequest req, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("Lead not found.");

        if (req.FullName != null) lead.FullName = req.FullName.Trim();
        if (req.Phone != null) lead.Phone = req.Phone.Trim();
        if (req.Email != null) lead.Email = req.Email.Trim().ToLowerInvariant();
        if (req.Source != null) lead.Source = req.Source;
        if (req.InterestLevel != null) lead.InterestLevel = req.InterestLevel;
        if (req.Notes != null) lead.Notes = req.Notes;
        if (req.AssignedTo.HasValue) lead.AssignedTo = req.AssignedTo;
        if (req.TrialAt.HasValue) lead.TrialAt = req.TrialAt;
        if (req.NextFollowUpAt.HasValue) lead.NextFollowUpAt = req.NextFollowUpAt;
        lead.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<LeadDto> ChangeStageAsync(Guid id, ChangeStageRequest req, CancellationToken ct)
    {
        if (!ValidStages.Contains(req.Stage))
            throw new BadRequestException($"Invalid stage. Valid: {string.Join(",", ValidStages)}");

        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("Lead not found.");

        var oldStage = lead.Stage;
        lead.Stage = req.Stage;
        if (req.Stage == "lost") lead.LostReason = req.LostReason;
        lead.UpdatedAt = DateTime.UtcNow;

        db.LeadActivities.Add(new LeadActivity
        {
            Id = Guid.NewGuid(),
            LeadId = lead.Id,
            ActorId = current.Id,
            Kind = "stage_change",
            Body = req.Stage == "lost" && !string.IsNullOrWhiteSpace(req.LostReason)
                ? $"Stage: {oldStage} → {req.Stage} (reason: {req.LostReason})"
                : $"Stage: {oldStage} → {req.Stage}",
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<LeadActivityDto> AddActivityAsync(Guid leadId, AddActivityRequest req, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct)
            ?? throw new NotFoundException("Lead not found.");

        var activity = new LeadActivity
        {
            Id = Guid.NewGuid(),
            LeadId = leadId,
            ActorId = current.Id,
            Kind = req.Kind,
            Body = req.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.LeadActivities.Add(activity);
        lead.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var actorName = await db.Profiles.Where(p => p.Id == current.Id).Select(p => p.FullName).FirstOrDefaultAsync(ct) ?? "You";
        return new LeadActivityDto
        {
            Id = activity.Id,
            LeadId = activity.LeadId,
            ActorId = activity.ActorId,
            ActorName = actorName,
            Kind = activity.Kind,
            Body = activity.Body,
            CreatedAt = activity.CreatedAt,
        };
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var rows = await db.Leads.Where(l => l.Id == id).ExecuteDeleteAsync(ct);
        if (rows == 0) throw new NotFoundException("Lead not found.");
    }

    public async Task<CrmStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var rows = await db.Leads
            .GroupBy(l => l.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStage = rows.ToDictionary(r => r.Stage, r => r.Count);
        var total = rows.Sum(r => r.Count);
        var converted = byStage.GetValueOrDefault("converted", 0);
        var now = DateTime.UtcNow;
        var overdue = await db.Leads.CountAsync(l =>
            l.NextFollowUpAt != null &&
            l.NextFollowUpAt < now &&
            l.Stage != "converted" &&
            l.Stage != "lost", ct);

        return new CrmStatsDto
        {
            New = byStage.GetValueOrDefault("new", 0),
            Contacted = byStage.GetValueOrDefault("contacted", 0),
            TrialScheduled = byStage.GetValueOrDefault("trial_scheduled", 0),
            Converted = converted,
            Lost = byStage.GetValueOrDefault("lost", 0),
            Total = total,
            ConversionPct = total > 0 ? (int)Math.Round(converted * 100.0 / total) : 0,
            OverdueFollowUps = overdue,
        };
    }

    private static LeadDto ToDto(Lead l, Dictionary<Guid, string> names, Dictionary<Guid, int> counts) => new()
    {
        Id = l.Id,
        FullName = l.FullName,
        Phone = l.Phone,
        Email = l.Email,
        Source = l.Source,
        Stage = l.Stage,
        InterestLevel = l.InterestLevel,
        Notes = l.Notes,
        AssignedTo = l.AssignedTo,
        AssignedToName = l.AssignedTo.HasValue ? names.GetValueOrDefault(l.AssignedTo.Value) : null,
        ConvertedProfileId = l.ConvertedProfileId,
        TrialAt = l.TrialAt,
        NextFollowUpAt = l.NextFollowUpAt,
        LostReason = l.LostReason,
        CreatedAt = l.CreatedAt,
        UpdatedAt = l.UpdatedAt,
        ActivityCount = counts.GetValueOrDefault(l.Id, 0),
    };
}
