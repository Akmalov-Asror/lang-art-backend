using System.Text.Json;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Writing.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Writing;

public class WritingService
{
    private const int XpPerWritingSubmission = 30;

    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Features.Notifications.NotificationsService _notify;
    private readonly ILogger<WritingService> _logger;

    public WritingService(
        AppDbContext db,
        IServiceScopeFactory scopeFactory,
        Features.Notifications.NotificationsService notify,
        ILogger<WritingService> logger)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _notify = notify;
        _logger = logger;
    }

    public async Task<WritingSubmissionDto> SubmitAsync(Guid userId, SubmitWritingRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            throw new BadRequestException("Text is required");

        var content = await _db.LessonContent.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ContentId, ct)
            ?? throw new NotFoundException("Content not found");

        string promptText = TryReadString(content.ContentPayload, "prompt") ?? string.Empty;
        int? minWordCount = TryReadInt(content.ContentPayload, "min_word_count");

        var wordCount = req.Text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

        var submission = new WritingSubmission
        {
            UserId = userId,
            ContentId = req.ContentId,
            Text = req.Text,
            WordCount = wordCount,
            Status = "submitted",
        };
        _db.WritingSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);

        // Fire-and-forget background grading.
        var submissionId = submission.Id;
        var lessonId = content.LessonId;
        _ = Task.Run(() => GradeInBackgroundAsync(submissionId, lessonId, promptText, req.Text, minWordCount, userId), CancellationToken.None);

        return await ToDtoAsync(submission, ct);
    }

    private async Task GradeInBackgroundAsync(
        Guid submissionId,
        Guid lessonId,
        string promptText,
        string text,
        int? minWordCount,
        Guid userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ai = scope.ServiceProvider.GetRequiredService<IAiWritingGradingService>();
        var gamification = scope.ServiceProvider.GetRequiredService<Features.Gamification.IGamificationService>();
        var notify = scope.ServiceProvider.GetRequiredService<Features.Notifications.NotificationsService>();
        var ct = CancellationToken.None;

        try
        {
            var grade = await ai.GradeAsync(text, promptText, minWordCount, ct);

            var sub = await db.WritingSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId, ct);
            if (sub is null)
            {
                _logger.LogWarning("Background grading: submission {Id} disappeared", submissionId);
                return;
            }
            sub.AiGradeTaskAchievement = grade.TaskAchievement;
            sub.AiGradeCoherence = grade.Coherence;
            sub.AiGradeGrammar = grade.Grammar;
            sub.AiGradeVocabulary = grade.Vocabulary;
            sub.AiTotal = grade.Total;
            sub.AiFeedback = grade.Feedback;
            sub.AiGradedAt = DateTime.UtcNow;
            sub.FinalGrade = grade.Total;
            sub.Status = "ai_graded";
            await db.SaveChangesAsync(ct);

            await gamification.AwardXpAsync(userId, XpReason.WritingCompleted, XpPerWritingSubmission, submissionId, ct);
            try { await gamification.RecordActivityAsync(userId, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "RecordActivity failed"); }
            try { _ = await gamification.EvaluateBadgesAsync(userId, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "EvaluateBadges failed"); }

            try
            {
                await notify.NotifyAsync(userId, "writing_graded",
                    "Your writing submission was graded",
                    $"AI score: {grade.Total}/100. Open to see feedback.",
                    $"/learn/lesson/{lessonId}");
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Notify failed"); }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background writing grading failed for submission {Id}", submissionId);
        }
    }

    public async Task<IReadOnlyList<WritingSubmissionDto>> ListMineAsync(Guid userId, CancellationToken ct)
    {
        var rows = await _db.WritingSubmissions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Include(s => s.User)
            .Include(s => s.Teacher)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<WritingSubmissionDto>> ListReviewQueueAsync(CancellationToken ct)
    {
        var rows = await _db.WritingSubmissions
            .AsNoTracking()
            .Where(s => s.Status == "ai_graded")
            .OrderBy(s => s.CreatedAt)
            .Include(s => s.User)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<WritingSubmissionDto> GetByIdAsync(Guid id, Guid currentUserId, string currentRole, CancellationToken ct)
    {
        var s = await _db.WritingSubmissions
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Teacher)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Submission not found");
        if (currentRole == "student" && s.UserId != currentUserId)
        {
            throw new ForbiddenException("You can only view your own submissions");
        }
        return ToDto(s);
    }

    public async Task<WritingSubmissionDto> ReviewAsync(Guid id, Guid teacherId, TeacherWritingReviewRequest req, CancellationToken ct)
    {
        var s = await _db.WritingSubmissions.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Submission not found");

        s.TeacherId = teacherId;
        s.TeacherGradeTotal = req.Grade;
        s.TeacherFeedback = req.Feedback;
        s.TeacherReviewedAt = DateTime.UtcNow;
        s.FinalGrade = req.Grade;
        s.Status = "teacher_reviewed";
        await _db.SaveChangesAsync(ct);

        try
        {
            await _notify.NotifyAsync(s.UserId, "writing_reviewed",
                "Your teacher reviewed your writing submission",
                $"Final grade: {req.Grade}/100",
                null);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Notify failed"); }

        return await ToDtoAsync(s, ct);
    }

    private static string? TryReadString(JsonDocument doc, string property)
    {
        try
        {
            return doc.RootElement.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        }
        catch { return null; }
    }

    private static int? TryReadInt(JsonDocument doc, string property)
    {
        try
        {
            return doc.RootElement.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32()
                : null;
        }
        catch { return null; }
    }

    private async Task<WritingSubmissionDto> ToDtoAsync(WritingSubmission s, CancellationToken ct)
    {
        var userName = await _db.Profiles.AsNoTracking()
            .Where(p => p.Id == s.UserId)
            .Select(p => p.FullName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
        var teacherName = s.TeacherId.HasValue
            ? await _db.Profiles.AsNoTracking()
                .Where(p => p.Id == s.TeacherId.Value)
                .Select(p => (string?)p.FullName)
                .FirstOrDefaultAsync(ct)
            : null;
        return new WritingSubmissionDto
        {
            Id = s.Id,
            UserId = s.UserId,
            UserName = userName,
            ContentId = s.ContentId,
            Text = s.Text,
            WordCount = s.WordCount,
            AiGradeTaskAchievement = s.AiGradeTaskAchievement,
            AiGradeCoherence = s.AiGradeCoherence,
            AiGradeGrammar = s.AiGradeGrammar,
            AiGradeVocabulary = s.AiGradeVocabulary,
            AiTotal = s.AiTotal,
            AiFeedback = s.AiFeedback,
            AiGradedAt = s.AiGradedAt,
            TeacherId = s.TeacherId,
            TeacherName = teacherName,
            TeacherGradeTotal = s.TeacherGradeTotal,
            TeacherFeedback = s.TeacherFeedback,
            TeacherReviewedAt = s.TeacherReviewedAt,
            FinalGrade = s.FinalGrade,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
        };
    }

    private static WritingSubmissionDto ToDto(WritingSubmission s) => new()
    {
        Id = s.Id,
        UserId = s.UserId,
        UserName = s.User?.FullName ?? string.Empty,
        ContentId = s.ContentId,
        Text = s.Text,
        WordCount = s.WordCount,
        AiGradeTaskAchievement = s.AiGradeTaskAchievement,
        AiGradeCoherence = s.AiGradeCoherence,
        AiGradeGrammar = s.AiGradeGrammar,
        AiGradeVocabulary = s.AiGradeVocabulary,
        AiTotal = s.AiTotal,
        AiFeedback = s.AiFeedback,
        AiGradedAt = s.AiGradedAt,
        TeacherId = s.TeacherId,
        TeacherName = s.Teacher?.FullName,
        TeacherGradeTotal = s.TeacherGradeTotal,
        TeacherFeedback = s.TeacherFeedback,
        TeacherReviewedAt = s.TeacherReviewedAt,
        FinalGrade = s.FinalGrade,
        Status = s.Status,
        CreatedAt = s.CreatedAt,
    };
}
