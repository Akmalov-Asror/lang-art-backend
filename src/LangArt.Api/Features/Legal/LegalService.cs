using System.Security.Cryptography;
using System.Text;
using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Legal.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Legal;

public class LegalService(AppDbContext db, ICurrentUser current, IHttpContextAccessor http)
{
    private static readonly HashSet<string> ValidKinds = new() { "public_offer", "terms", "privacy", "refund" };

    public async Task<List<LegalDocumentDto>> ListAsync(string? kind, CancellationToken ct)
    {
        var q = db.LegalDocuments.AsQueryable();
        if (!string.IsNullOrWhiteSpace(kind)) q = q.Where(d => d.Kind == kind);
        var rows = await q.OrderByDescending(d => d.EffectiveFrom).ToListAsync(ct);

        var docIds = rows.Select(r => r.Id).ToList();
        var myAccepted = await db.LegalAcceptances
            .Where(a => a.UserId == current.Id && docIds.Contains(a.DocumentId))
            .Select(a => a.DocumentId)
            .ToListAsync(ct);
        var acceptedSet = myAccepted.ToHashSet();

        return rows.Select(d => ToDto(d, acceptedSet.Contains(d.Id))).ToList();
    }

    public async Task<LegalDocumentDto> GetAsync(Guid id, CancellationToken ct)
    {
        var d = await db.LegalDocuments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Document not found.");
        var accepted = await db.LegalAcceptances.AnyAsync(a => a.UserId == current.Id && a.DocumentId == id, ct);
        return ToDto(d, accepted);
    }

    public async Task<PendingAcceptanceDto> GetPendingForMeAsync(CancellationToken ct)
    {
        var currentDocs = await db.LegalDocuments
            .Where(d => d.IsCurrent && d.EffectiveFrom <= DateTime.UtcNow)
            .ToListAsync(ct);

        var ids = currentDocs.Select(d => d.Id).ToList();
        var acceptedIds = (await db.LegalAcceptances
            .Where(a => a.UserId == current.Id && ids.Contains(a.DocumentId))
            .Select(a => a.DocumentId)
            .ToListAsync(ct)).ToHashSet();

        return new PendingAcceptanceDto
        {
            Documents = currentDocs
                .Where(d => !acceptedIds.Contains(d.Id))
                .Select(d => ToDto(d, false))
                .ToList(),
        };
    }

    public async Task<LegalDocumentDto> PublishAsync(PublishDocumentRequest req, CancellationToken ct)
    {
        if (!ValidKinds.Contains(req.Kind))
            throw new BadRequestException($"Invalid kind. Valid: {string.Join(",", ValidKinds)}");

        // Mark prior current docs of this kind as not current
        await db.LegalDocuments
            .Where(d => d.Kind == req.Kind && d.IsCurrent)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsCurrent, false), ct);

        var nextVersion = (await db.LegalDocuments
            .Where(d => d.Kind == req.Kind)
            .MaxAsync(d => (int?)d.Version, ct) ?? 0) + 1;

        var doc = new LegalDocument
        {
            Id = Guid.NewGuid(),
            Kind = req.Kind,
            Version = nextVersion,
            Title = req.Title.Trim(),
            BodyMarkdown = req.BodyMarkdown,
            IsCurrent = true,
            CreatedBy = current.Id,
            EffectiveFrom = req.EffectiveFrom ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        db.LegalDocuments.Add(doc);
        await db.SaveChangesAsync(ct);

        return ToDto(doc, false);
    }

    public async Task<LegalAcceptanceDto> AcceptAsync(AcceptDocumentRequest req, CancellationToken ct)
    {
        var doc = await db.LegalDocuments.FirstOrDefaultAsync(d => d.Id == req.DocumentId, ct)
            ?? throw new NotFoundException("Document not found.");

        var existing = await db.LegalAcceptances
            .FirstOrDefaultAsync(a => a.UserId == current.Id && a.DocumentId == req.DocumentId, ct);
        if (existing != null) return ToAcceptanceDto(existing);

        var ctx = http.HttpContext;
        var ua = ctx?.Request.Headers.UserAgent.ToString();
        var ip = ctx?.Connection.RemoteIpAddress?.ToString();

        var hash = Hash(doc.BodyMarkdown);

        var acc = new LegalAcceptance
        {
            Id = Guid.NewGuid(),
            UserId = current.Id,
            DocumentId = doc.Id,
            Kind = doc.Kind,
            Version = doc.Version,
            ContentHash = hash,
            UserAgent = ua,
            IpAddress = ip,
            AcceptedAt = DateTime.UtcNow,
        };
        db.LegalAcceptances.Add(acc);
        await db.SaveChangesAsync(ct);
        return ToAcceptanceDto(acc);
    }

    public async Task<List<LegalAcceptanceDto>> ListAcceptancesAsync(Guid documentId, CancellationToken ct)
    {
        var rows = await db.LegalAcceptances
            .Where(a => a.DocumentId == documentId)
            .OrderByDescending(a => a.AcceptedAt)
            .Take(500)
            .ToListAsync(ct);

        var ids = rows.Select(r => r.UserId).Distinct().ToList();
        var names = await db.Profiles
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.FullName })
            .ToDictionaryAsync(p => p.Id, p => p.FullName, ct);

        return rows.Select(a => new LegalAcceptanceDto
        {
            Id = a.Id,
            UserId = a.UserId,
            UserName = names.GetValueOrDefault(a.UserId, "Unknown"),
            DocumentId = a.DocumentId,
            Kind = a.Kind,
            Version = a.Version,
            UserAgent = a.UserAgent,
            IpAddress = a.IpAddress,
            AcceptedAt = a.AcceptedAt,
        }).ToList();
    }

    private static LegalDocumentDto ToDto(LegalDocument d, bool acceptedByMe) => new()
    {
        Id = d.Id,
        Kind = d.Kind,
        Version = d.Version,
        Title = d.Title,
        BodyMarkdown = d.BodyMarkdown,
        IsCurrent = d.IsCurrent,
        EffectiveFrom = d.EffectiveFrom,
        CreatedAt = d.CreatedAt,
        AcceptedByMe = acceptedByMe,
    };

    private static LegalAcceptanceDto ToAcceptanceDto(LegalAcceptance a) => new()
    {
        Id = a.Id,
        UserId = a.UserId,
        DocumentId = a.DocumentId,
        Kind = a.Kind,
        Version = a.Version,
        UserAgent = a.UserAgent,
        IpAddress = a.IpAddress,
        AcceptedAt = a.AcceptedAt,
    };

    private static string Hash(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
