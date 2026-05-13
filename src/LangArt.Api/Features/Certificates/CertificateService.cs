using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LangArt.Api.Features.Certificates;

public class CertificateService
{
    private readonly AppDbContext _db;

    static CertificateService()
    {
        // QuestPDF requires opting into the Community license at startup. Free for
        // small businesses and OSS; see https://www.questpdf.com/license/community.html
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public CertificateService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(byte[] Pdf, string FileName)> GenerateAsync(Guid userId, Guid courseId)
    {
        var user = await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == userId)
                   ?? throw new NotFoundException("User not found");
        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId)
                     ?? throw new NotFoundException("Course not found");

        // Verify completion: lessons in the course vs lessons in lesson_completions for this user.
        var lessonIds = await _db.Lessons
            .Where(l => l.Module.CourseId == courseId)
            .Select(l => l.Id)
            .ToListAsync();
        if (lessonIds.Count == 0)
            throw new BadRequestException("Course has no lessons to complete");

        var doneCount = await _db.LessonCompletions
            .CountAsync(c => c.UserId == userId && lessonIds.Contains(c.LessonId));

        if (doneCount < lessonIds.Count)
        {
            throw new BadRequestException($"Course is not yet complete ({doneCount}/{lessonIds.Count} lessons).");
        }

        var completedAt = await _db.LessonCompletions
            .Where(c => c.UserId == userId && lessonIds.Contains(c.LessonId))
            .OrderByDescending(c => c.CompletedAt)
            .Select(c => c.CompletedAt)
            .FirstAsync();

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(14).FontColor("#203d60"));

                page.Content().Column(col =>
                {
                    col.Spacing(20);

                    col.Item().AlignCenter().Text("Certificate of Completion")
                        .FontSize(36).Bold().FontColor("#203d60");

                    col.Item().AlignCenter().Text("This is to certify that").FontSize(14).Italic();

                    col.Item().AlignCenter().Text(user.FullName)
                        .FontSize(32).FontColor("#D4AF37");

                    col.Item().AlignCenter().Text("has successfully completed the course").FontSize(14).Italic();

                    col.Item().AlignCenter().Text(course.Title)
                        .FontSize(26).Bold();

                    if (!string.IsNullOrWhiteSpace(course.Description))
                    {
                        col.Item().AlignCenter().PaddingHorizontal(60).Text(course.Description)
                            .FontSize(12).FontColor("#203d60");
                    }

                    col.Item().PaddingTop(20).AlignCenter().Text($"Awarded on {completedAt:MMMM d, yyyy}")
                        .FontSize(13);

                    col.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text("LangArt LMS").FontSize(12).Bold();
                            c.Item().AlignCenter().Text("issued by the LangArt team").FontSize(10).FontColor("#999999");
                        });
                    });

                    col.Item().AlignCenter().Text($"Certificate ID: {Guid.NewGuid()}").FontSize(8).FontColor("#999999");
                });
            });
        }).GeneratePdf();

        var safeName = $"{course.Title.Replace(' ', '_').Replace('—', '-')}_{user.FullName.Replace(' ', '_')}.pdf";
        return (pdf, safeName);
    }
}
