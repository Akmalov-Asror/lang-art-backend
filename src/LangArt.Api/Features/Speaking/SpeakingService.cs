using System.Text.Json;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Speaking.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Speaking;

public class SpeakingService
{
    private const int XpPerSpeakingSubmission = 25;

    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Features.Notifications.NotificationsService _notify;
    private readonly ILogger<SpeakingService> _logger;

    public SpeakingService(
        AppDbContext db,
        IServiceScopeFactory scopeFactory,
        Features.Notifications.NotificationsService notify,
        ILogger<SpeakingService> logger)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _notify = notify;
        _logger = logger;
    }

    public async Task<SpeakingSubmissionDto> SubmitAsync(Guid userId, SubmitSpeakingRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.AudioUrl))
            throw new BadRequestException("audioUrl is required");

        var content = await _db.LessonContent.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ContentId, ct)
            ?? throw new NotFoundException("Content not found");

        // Extract a prompt text from the speaking exercise payload (if available)
        // so AI grading has context for tailored feedback.
        string promptText = TryReadString(content.ContentPayload, "prompt_text") ?? string.Empty;

        var submission = new SpeakingSubmission
        {
            UserId = userId,
            ContentId = req.ContentId,
            AudioUrl = req.AudioUrl,
            Status = "submitted",
        };
        _db.SpeakingSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);

        // Fire-and-forget background grading. Student gets an instant response;
        // SignalR push + bell notification fires when grading is done. We deliberately
        // create a NEW scope inside the background task so the scoped DbContext
        // bound to this request can be disposed independently.
        var submissionId = submission.Id;
        var lessonId = content.LessonId;
        _ = Task.Run(() => GradeInBackgroundAsync(submissionId, lessonId, promptText, req.AudioUrl, userId), CancellationToken.None);

        return await ToDtoAsync(submission, ct);
    }

    private async Task GradeInBackgroundAsync(
        Guid submissionId,
        Guid lessonId,
        string promptText,
        string audioUrl,
        Guid userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ai = scope.ServiceProvider.GetRequiredService<IAiSpeechGradingService>();
        var gamification = scope.ServiceProvider.GetRequiredService<Features.Gamification.IGamificationService>();
        var notify = scope.ServiceProvider.GetRequiredService<Features.Notifications.NotificationsService>();
        var ct = CancellationToken.None;

        try
        {
            var grade = await ai.GradeAsync(audioUrl, promptText, ct);

            var sub = await db.SpeakingSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId, ct);
            if (sub is null)
            {
                _logger.LogWarning("Background grading: submission {Id} disappeared", submissionId);
                return;
            }
            sub.Transcript = grade.Transcript;
            sub.AiGradePronunciation = grade.Pronunciation;
            sub.AiGradeFluency = grade.Fluency;
            sub.AiGradeGrammar = grade.Grammar;
            sub.AiGradeVocabulary = grade.Vocabulary;
            sub.AiTotal = grade.Total;
            sub.AiFeedback = grade.Feedback;
            sub.AiGradedAt = DateTime.UtcNow;
            sub.FinalGrade = grade.Total;
            sub.Status = "ai_graded";
            await db.SaveChangesAsync(ct);

            // Idempotent — submission.Id as source means re-grading would no-op.
            await gamification.AwardXpAsync(userId, XpReason.SpeakingCompleted, XpPerSpeakingSubmission, submissionId, ct);
            try { await gamification.RecordActivityAsync(userId, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "RecordActivity failed"); }
            try { _ = await gamification.EvaluateBadgesAsync(userId, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "EvaluateBadges failed"); }

            try
            {
                await notify.NotifyAsync(userId, "speaking_graded",
                    "Your speaking submission was graded",
                    $"AI score: {grade.Total}/100. Open to see feedback.",
                    $"/learn/lesson/{lessonId}");
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Notify failed"); }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background AI grading failed for submission {Id}", submissionId);
            // Leave the row in "submitted" so a teacher can still review it manually.
        }
    }

    public async Task<IReadOnlyList<SpeakingSubmissionDto>> ListMineAsync(Guid userId, CancellationToken ct)
    {
        var rows = await _db.SpeakingSubmissions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Include(s => s.User)
            .Include(s => s.Teacher)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<SpeakingSubmissionDto>> ListReviewQueueAsync(CancellationToken ct)
    {
        // Pending review = AI-graded but not yet teacher-reviewed.
        var rows = await _db.SpeakingSubmissions
            .AsNoTracking()
            .Where(s => s.Status == "ai_graded")
            .OrderBy(s => s.CreatedAt)
            .Include(s => s.User)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<SpeakingSubmissionDto> GetByIdAsync(Guid id, Guid currentUserId, string currentRole, CancellationToken ct)
    {
        var s = await _db.SpeakingSubmissions
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

    public async Task<SpeakingSubmissionDto> ReviewAsync(Guid id, Guid teacherId, TeacherReviewRequest req, CancellationToken ct)
    {
        var s = await _db.SpeakingSubmissions.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Submission not found");

        s.TeacherId = teacherId;
        s.TeacherGradeTotal = req.Grade;
        s.TeacherFeedback = req.Feedback;
        s.TeacherReviewedAt = DateTime.UtcNow;
        s.FinalGrade = req.Grade;
        s.Status = "teacher_reviewed";
        await _db.SaveChangesAsync(ct);

        await SafeNotifyAsync(s.UserId, "speaking_reviewed",
            "Your teacher reviewed your speaking submission",
            $"Final grade: {req.Grade}/100",
            null);

        return await ToDtoAsync(s, ct);
    }

    private async Task SafeNotifyAsync(Guid userId, string kind, string title, string body, string? linkUrl)
    {
        try
        {
            await _notify.NotifyAsync(userId, kind, title, body, linkUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification dispatch failed for {UserId}", userId);
        }
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

    private async Task<SpeakingSubmissionDto> ToDtoAsync(SpeakingSubmission s, CancellationToken ct)
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
        return new SpeakingSubmissionDto
        {
            Id = s.Id,
            UserId = s.UserId,
            UserName = userName,
            ContentId = s.ContentId,
            AudioUrl = s.AudioUrl,
            Transcript = s.Transcript,
            AiGradePronunciation = s.AiGradePronunciation,
            AiGradeFluency = s.AiGradeFluency,
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

    private static SpeakingSubmissionDto ToDto(SpeakingSubmission s) => new()
    {
        Id = s.Id,
        UserId = s.UserId,
        UserName = s.User?.FullName ?? string.Empty,
        ContentId = s.ContentId,
        AudioUrl = s.AudioUrl,
        Transcript = s.Transcript,
        AiGradePronunciation = s.AiGradePronunciation,
        AiGradeFluency = s.AiGradeFluency,
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
