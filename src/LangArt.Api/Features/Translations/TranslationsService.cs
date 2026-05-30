using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Translations.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Translations;

public class TranslationsService
{
    private readonly AppDbContext _db;

    public TranslationsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TranslationDto>> ListForContentAsync(Guid contentId, CancellationToken ct)
    {
        var contentExists = await _db.LessonContent.AnyAsync(c => c.Id == contentId, ct);
        if (!contentExists) throw new NotFoundException("Content not found");

        var rows = await _db.LessonContentTranslations
            .AsNoTracking()
            .Where(t => t.ContentId == contentId)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<TranslationDto> UpsertAsync(Guid contentId, UpsertTranslationRequest req, CancellationToken ct)
    {
        var contentExists = await _db.LessonContent.AnyAsync(c => c.Id == contentId, ct);
        if (!contentExists) throw new NotFoundException("Content not found");

        var existing = await _db.LessonContentTranslations
            .FirstOrDefaultAsync(t => t.ContentId == contentId && t.Language == req.Language, ct);

        if (existing is null)
        {
            existing = new LessonContentTranslation
            {
                ContentId = contentId,
                Language = req.Language,
                BodyMarkdown = req.BodyMarkdown,
                VideoUrl = req.VideoUrl,
                SubtitleUrl = req.SubtitleUrl,
                Script = req.Script,
            };
            _db.LessonContentTranslations.Add(existing);
        }
        else
        {
            existing.BodyMarkdown = req.BodyMarkdown;
            existing.VideoUrl = req.VideoUrl;
            existing.SubtitleUrl = req.SubtitleUrl;
            existing.Script = req.Script;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(existing);
    }

    public async Task DeleteAsync(Guid contentId, string language, CancellationToken ct)
    {
        var deleted = await _db.LessonContentTranslations
            .Where(t => t.ContentId == contentId && t.Language == language)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new NotFoundException("Translation not found");
    }

    private static TranslationDto ToDto(LessonContentTranslation t) => new()
    {
        Id = t.Id,
        ContentId = t.ContentId,
        Language = t.Language,
        BodyMarkdown = t.BodyMarkdown,
        VideoUrl = t.VideoUrl,
        SubtitleUrl = t.SubtitleUrl,
        Script = t.Script,
        UpdatedAt = t.UpdatedAt,
    };
}
