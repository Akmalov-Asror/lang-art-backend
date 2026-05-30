using System.Security.Cryptography;
using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.LaDollar;
using LangArt.Api.Features.Referrals.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Referrals;

public class ReferralsService(AppDbContext db, ICurrentUser current, ILaDollarService laDollar)
{
    private const int DefaultReferralReward = 50;

    public async Task<MyReferralStatusDto> GetMyStatusAsync(CancellationToken ct)
    {
        var me = await db.Profiles.FirstOrDefaultAsync(p => p.Id == current.Id, ct)
            ?? throw new NotFoundException("Profile not found.");

        if (string.IsNullOrWhiteSpace(me.ReferralCode))
        {
            me.ReferralCode = await EnsureUniqueCodeAsync(me.FullName, ct);
            await db.SaveChangesAsync(ct);
        }

        var entries = await db.Referrals
            .Where(r => r.ReferrerId == current.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var refIds = entries.Where(e => e.RefereeProfileId.HasValue).Select(e => e.RefereeProfileId!.Value).Distinct().ToList();
        var leadIds = entries.Where(e => e.LeadId.HasValue).Select(e => e.LeadId!.Value).Distinct().ToList();

        var refNames = refIds.Count > 0
            ? await db.Profiles.Where(p => refIds.Contains(p.Id)).Select(p => new { p.Id, p.FullName }).ToDictionaryAsync(p => p.Id, p => p.FullName, ct)
            : new Dictionary<Guid, string>();
        var leadNames = leadIds.Count > 0
            ? await db.Leads.Where(l => leadIds.Contains(l.Id)).Select(l => new { l.Id, l.FullName }).ToDictionaryAsync(l => l.Id, l => l.FullName, ct)
            : new Dictionary<Guid, string>();

        return new MyReferralStatusDto
        {
            Code = me.ReferralCode!,
            TotalReferred = entries.Count,
            Converted = entries.Count(e => e.Status == "converted" || e.Status == "rewarded"),
            Rewarded = entries.Count(e => e.Status == "rewarded"),
            TotalEarnedLaDollars = entries.Where(e => e.Status == "rewarded").Sum(e => e.RewardLaDollars),
            Recent = entries.Select(e => new ReferralEntryDto
            {
                Id = e.Id,
                Status = e.Status,
                RefereeName = e.RefereeProfileId.HasValue ? refNames.GetValueOrDefault(e.RefereeProfileId.Value) : null,
                LeadName = e.LeadId.HasValue ? leadNames.GetValueOrDefault(e.LeadId.Value) : null,
                RewardLaDollars = e.RewardLaDollars,
                CreatedAt = e.CreatedAt,
                RewardedAt = e.RewardedAt,
            }).ToList(),
        };
    }

    /// <summary>Admin: attach a referrer code to a lead.</summary>
    public async Task AttachToLeadAsync(Guid leadId, AttachReferralRequest req, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct)
            ?? throw new NotFoundException("Lead not found.");

        var code = req.Code.Trim().ToUpperInvariant();
        var referrer = await db.Profiles.FirstOrDefaultAsync(p => p.ReferralCode == code, ct)
            ?? throw new NotFoundException("Referral code not found.");

        if (referrer.Id == lead.ConvertedProfileId)
            throw new BadRequestException("A user cannot refer themselves.");

        // Idempotent: one referral per (referrer, lead)
        var existing = await db.Referrals.FirstOrDefaultAsync(r => r.ReferrerId == referrer.Id && r.LeadId == leadId, ct);
        if (existing != null) return;

        db.Referrals.Add(new Referral
        {
            Id = Guid.NewGuid(),
            ReferrerId = referrer.Id,
            LeadId = leadId,
            Status = lead.Stage == "converted" ? "converted" : "pending",
            RewardLaDollars = Math.Max(0, req.RewardLaDollars),
            CreatedAt = DateTime.UtcNow,
            ConvertedAt = lead.Stage == "converted" ? DateTime.UtcNow : null,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Admin: mark a referral rewarded and pay out LA Dollar.</summary>
    public async Task RewardAsync(Guid referralId, RewardReferralRequest req, CancellationToken ct)
    {
        var r = await db.Referrals.FirstOrDefaultAsync(x => x.Id == referralId, ct)
            ?? throw new NotFoundException("Referral not found.");
        if (r.Status == "rewarded") return;
        if (req.RewardLaDollars.HasValue) r.RewardLaDollars = Math.Max(0, req.RewardLaDollars.Value);

        r.Status = "rewarded";
        r.RewardedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (r.RewardLaDollars > 0)
        {
            await laDollar.AwardAsync(
                r.ReferrerId,
                "admin_adjustment",
                r.RewardLaDollars,
                r.Id,
                "Referral reward",
                ct);
        }
    }

    public async Task<List<AlumniDto>> ListAlumniAsync(CancellationToken ct)
    {
        return await db.Profiles
            .Where(p => p.IsAlumni)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(200)
            .Select(p => new AlumniDto
            {
                Id = p.Id,
                FullName = p.FullName,
                AvatarUrl = p.AvatarUrl,
                Note = p.AlumniNote,
                CreatedAt = p.CreatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task SetAlumniAsync(Guid profileId, SetAlumniRequest req, CancellationToken ct)
    {
        var rows = await db.Profiles
            .Where(p => p.Id == profileId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsAlumni, req.IsAlumni)
                .SetProperty(p => p.AlumniNote, req.Note), ct);
        if (rows == 0) throw new NotFoundException("Profile not found.");
    }

    private async Task<string> EnsureUniqueCodeAsync(string fullName, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            var code = GenerateCode(fullName);
            var taken = await db.Profiles.AnyAsync(p => p.ReferralCode == code, ct);
            if (!taken) return code;
        }
        return $"R-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    private static string GenerateCode(string fullName)
    {
        var initials = new string(fullName.Split(' ')
            .Where(s => s.Length > 0)
            .Take(2)
            .Select(s => char.ToUpperInvariant(s[0]))
            .ToArray());
        if (initials.Length == 0) initials = "LA";
        var rand = RandomNumberGenerator.GetInt32(1000, 9999);
        return $"{initials}-{rand}";
    }
}
