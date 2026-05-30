using System.Text.Json;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Progress.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Progress;

public class ProgressService
{
    private readonly AppDbContext _db;
    private readonly Features.Notifications.NotificationsService _notify;
    private readonly Features.Gamification.IGamificationService _gamification;
    private readonly Features.LaDollar.ILaDollarService _laDollar;

    public ProgressService(
        AppDbContext db,
        Features.Notifications.NotificationsService notify,
        Features.Gamification.IGamificationService gamification,
        Features.LaDollar.ILaDollarService laDollar)
    {
        _db = db;
        _notify = notify;
        _gamification = gamification;
        _laDollar = laDollar;
    }

    // ---------------- Completions ----------------

    public async Task<LessonCompletionResponse> MarkCompleteAsync(Guid userId, Guid lessonId)
    {
        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == lessonId);
        if (!lessonExists) throw new NotFoundException("Lesson not found");

        var existing = await _db.LessonCompletions.FirstOrDefaultAsync(c => c.UserId == userId && c.LessonId == lessonId);
        var isFirstCompletion = existing is null;
        if (existing is null)
        {
            existing = new LessonCompletion
            {
                UserId = userId,
                LessonId = lessonId,
                CompletedAt = DateTime.UtcNow,
            };
            _db.LessonCompletions.Add(existing);
            await _db.SaveChangesAsync();
        }

        // Gamification side-effects. AwardXp + RecordActivity + EvaluateBadges all
        // self-guard against duplicate awards; calling them on a repeat completion
        // is cheap and intentional (e.g. badge criteria that hadn't been met yet
        // might be met now due to other state).
        if (isFirstCompletion)
        {
            await _gamification.AwardXpAsync(userId, Data.Enums.XpReason.LessonCompleted, 20, lessonId, default);
            // Phase 4: also award LA Dollars (idempotent on lessonId source).
            await _laDollar.AwardAsync(userId, "lesson_completed", 5, lessonId, "Lesson completed", default);

            // Auto-unlock the next lesson in the course for this student so they
            // can proceed without waiting for a teacher to explicitly unlock it.
            await AutoUnlockNextLessonAsync(userId, lessonId);
        }
        await _gamification.RecordActivityAsync(userId, default);
        await _gamification.EvaluateBadgesAsync(userId, default);

        return new LessonCompletionResponse
        {
            UserId = existing.UserId,
            LessonId = existing.LessonId,
            CompletedAt = existing.CompletedAt,
        };
    }

    public async Task<List<LessonCompletionResponse>> ListCompletionsAsync(Guid userId)
    {
        var rows = await _db.LessonCompletions
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CompletedAt)
            .ToListAsync();

        return rows.Select(c => new LessonCompletionResponse
        {
            UserId = c.UserId,
            LessonId = c.LessonId,
            CompletedAt = c.CompletedAt,
        }).ToList();
    }

    public async Task<CompletedStatusResponse> IsCompletedAsync(Guid userId, Guid lessonId)
    {
        var done = await _db.LessonCompletions.AnyAsync(c => c.UserId == userId && c.LessonId == lessonId);
        return new CompletedStatusResponse { Completed = done };
    }

    // ---------------- Quiz results ----------------

    public async Task<QuizResultResponse> SubmitQuizResultAsync(Guid userId, SubmitQuizResultRequest dto)
    {
        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == dto.LessonId);
        if (!lessonExists) throw new NotFoundException("Lesson not found");

        QuizResult? existing = null;
        if (dto.ContentId.HasValue)
        {
            existing = await _db.QuizResults
                .Where(q => q.UserId == userId && q.ContentId == dto.ContentId.Value)
                .OrderByDescending(q => q.Score)
                .FirstOrDefaultAsync();
        }

        JsonDocument? mistakes = dto.MistakesLog.HasValue
            ? JsonDocument.Parse(dto.MistakesLog.Value.GetRawText())
            : null;

        bool passed = dto.Passed ??
            (dto.TotalQuestions > 0 && (double)dto.Score / dto.TotalQuestions >= 0.7);

        if (existing is not null && dto.Score > existing.Score)
        {
            existing.Score = dto.Score;
            existing.Passed = passed;
            existing.TotalQuestions = dto.TotalQuestions;
            existing.MistakesLog = mistakes ?? existing.MistakesLog;
            existing.TeacherFeedback = dto.TeacherFeedback ?? existing.TeacherFeedback;
            await _db.SaveChangesAsync();
            return ToQuizResultResponse(existing);
        }

        if (existing is not null && dto.Score <= existing.Score)
        {
            return ToQuizResultResponse(existing);
        }

        var record = new QuizResult
        {
            UserId = userId,
            LessonId = dto.LessonId,
            ContentId = dto.ContentId,
            Score = dto.Score,
            TotalQuestions = dto.TotalQuestions,
            Passed = passed,
            MistakesLog = mistakes,
            TeacherFeedback = dto.TeacherFeedback,
        };
        _db.QuizResults.Add(record);
        await _db.SaveChangesAsync();

        // Gamification — first-time-per-submission rewards. We use record.Id as the
        // ledger source_id so re-submitting the same QuizResult row never double-awards;
        // independent attempts produce different rows and award independently.
        if (passed)
        {
            // Reading exercises award a different reason (mirrors quiz flow otherwise).
            var exerciseType = dto.ContentId.HasValue
                ? await _db.LessonContent
                    .Where(c => c.Id == dto.ContentId.Value)
                    .Select(c => c.ExerciseType)
                    .FirstOrDefaultAsync()
                : null;
            var primaryReason = exerciseType == "reading"
                ? Data.Enums.XpReason.ReadingCompleted
                : Data.Enums.XpReason.QuizPassed;
            await _gamification.AwardXpAsync(userId, primaryReason, 30, record.Id, default);
            var isPerfect = dto.TotalQuestions > 0 && dto.Score == dto.TotalQuestions;
            if (isPerfect)
            {
                await _gamification.AwardXpAsync(userId, Data.Enums.XpReason.QuizPerfectBonus, 20, record.Id, default);
            }
            await _gamification.RecordActivityAsync(userId, default);
            await _gamification.EvaluateBadgesAsync(userId, default);

            // Phase 4: LA Dollar rewards for passing an exercise. record.Id is the
            // unique source so re-submitting the same QuizResult never double-pays.
            await _laDollar.AwardAsync(userId, "quiz_passed", 10, record.Id, "Exercise passed", default);
            if (isPerfect)
            {
                await _laDollar.AwardAsync(userId, "quiz_perfect_bonus", 5, record.Id, "Perfect score bonus", default);
            }
        }

        return ToQuizResultResponse(record);
    }

    /// <summary>
    /// When <paramref name="contentId"/> is supplied: return the user's best result for that content (or null).
    /// Otherwise: return all of the user's results (optionally filtered by lesson).
    /// </summary>
    public async Task<object?> GetQuizResultsAsync(Guid userId, Guid? lessonId, Guid? contentId)
    {
        if (contentId.HasValue)
        {
            var best = await _db.QuizResults
                .AsNoTracking()
                .Where(q => q.UserId == userId && q.ContentId == contentId.Value)
                .OrderByDescending(q => q.Score)
                .FirstOrDefaultAsync();
            return best is null ? null : ToQuizResultResponse(best);
        }

        var query = _db.QuizResults.AsNoTracking().Where(q => q.UserId == userId);
        if (lessonId.HasValue) query = query.Where(q => q.LessonId == lessonId.Value);
        var rows = await query.OrderByDescending(q => q.CreatedAt).ToListAsync();
        return rows.Select(ToQuizResultResponse).ToList();
    }

    // ---------------- Course progress ----------------

    public async Task<CourseProgressResponse> GetCourseProgressAsync(Guid userId, Guid courseId)
    {
        var course = await _db.Courses
            .AsNoTracking()
            .Include(c => c.Modules.OrderBy(m => m.OrderIndex))
                .ThenInclude(m => m.Lessons.OrderBy(l => l.OrderIndex))
            .FirstOrDefaultAsync(c => c.Id == courseId)
            ?? throw new NotFoundException("Course not found");

        var lessonIds = course.Modules.SelectMany(m => m.Lessons).Select(l => l.Id).ToList();
        var completions = await _db.LessonCompletions
            .AsNoTracking()
            .Where(c => c.UserId == userId && lessonIds.Contains(c.LessonId))
            .ToListAsync();
        var quiz = await _db.QuizResults
            .AsNoTracking()
            .Where(q => q.UserId == userId && lessonIds.Contains(q.LessonId))
            .ToListAsync();

        var completionsByLesson = completions.ToLookup(c => c.LessonId);
        var quizByLesson = quiz.ToLookup(q => q.LessonId);

        return new CourseProgressResponse
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            ThumbnailUrl = course.ThumbnailUrl,
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt,
            Modules = course.Modules.OrderBy(m => m.OrderIndex).Select(m => new ModuleProgressResponse
            {
                Id = m.Id,
                CourseId = m.CourseId,
                Title = m.Title,
                OrderIndex = m.OrderIndex,
                CreatedAt = m.CreatedAt,
                Lessons = m.Lessons.OrderBy(l => l.OrderIndex).Select(l => new LessonProgressResponse
                {
                    Id = l.Id,
                    ModuleId = l.ModuleId,
                    Title = l.Title,
                    OrderIndex = l.OrderIndex,
                    IsLocked = l.IsLocked,
                    CreatedAt = l.CreatedAt,
                    Completions = completionsByLesson[l.Id].Select(c => new LessonCompletionResponse
                    {
                        UserId = c.UserId,
                        LessonId = c.LessonId,
                        CompletedAt = c.CompletedAt,
                    }).ToList(),
                    QuizResults = quizByLesson[l.Id].Select(ToQuizResultResponse).ToList(),
                }).ToList(),
            }).ToList(),
        };
    }

    public async Task<CoursePercentageResponse> GetCoursePercentageAsync(Guid userId, Guid courseId)
    {
        var courseExists = await _db.Courses.AnyAsync(c => c.Id == courseId);
        if (!courseExists) throw new NotFoundException("Course not found");

        var lessonIds = await _db.Lessons
            .Where(l => l.Module.CourseId == courseId)
            .Select(l => l.Id)
            .ToListAsync();

        if (lessonIds.Count == 0) return new CoursePercentageResponse { Percentage = 0 };

        var done = await _db.LessonCompletions
            .Where(c => c.UserId == userId && lessonIds.Contains(c.LessonId))
            .CountAsync();

        var pct = (int)Math.Round((double)done / lessonIds.Count * 100);
        return new CoursePercentageResponse { Percentage = pct };
    }

    // ---------------- Lesson access (gating) ----------------

    public async Task UnlockAsync(Guid studentId, Guid lessonId, Guid? actorId)
    {
        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == lessonId);
        if (!lessonExists) throw new NotFoundException("Lesson not found");
        var studentExists = await _db.Profiles.AnyAsync(p => p.Id == studentId);
        if (!studentExists) throw new NotFoundException("Student not found");

        var existing = await _db.StudentLessonAccess
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.LessonId == lessonId);

        if (existing is null)
        {
            _db.StudentLessonAccess.Add(new StudentLessonAccess
            {
                StudentId = studentId,
                LessonId = lessonId,
                IsUnlocked = true,
                CreatedBy = actorId,
            });
        }
        else
        {
            existing.IsUnlocked = true;
            existing.UnlockedAt = DateTime.UtcNow;
            existing.CreatedBy = actorId ?? existing.CreatedBy;
        }
        await _db.SaveChangesAsync();

        var lesson = await _db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId);
        await _notify.NotifyAsync(
            studentId,
            "lesson_unlocked",
            "A lesson was unlocked for you",
            lesson is null ? null : $"\"{lesson.Title}\" is now available.",
            null);
    }

    public async Task LockAsync(Guid studentId, Guid lessonId)
    {
        var existing = await _db.StudentLessonAccess
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.LessonId == lessonId);
        if (existing is null) return;
        existing.IsUnlocked = false;
        await _db.SaveChangesAsync();
    }

    public async Task<List<Guid>> GetUnlockedLessonIdsAsync(Guid studentId)
    {
        return await _db.StudentLessonAccess
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && a.IsUnlocked)
            .Select(a => a.LessonId)
            .ToListAsync();
    }

    /// <summary>
    /// Called from MarkCompleteAsync — when a student finishes a lesson we
    /// automatically grant them access to the following lesson in course order.
    /// Looks first in the same module (order_index + 1); if that was the last
    /// lesson, looks for the first lesson of the next module. Honors the
    /// existing StudentLessonAccess pattern, so a teacher can still manually
    /// re-lock it via the existing API.
    /// </summary>
    private async Task AutoUnlockNextLessonAsync(Guid studentId, Guid currentLessonId)
    {
        var current = await _db.Lessons.AsNoTracking()
            .Where(l => l.Id == currentLessonId)
            .Select(l => new { l.Id, l.ModuleId, l.OrderIndex, CourseId = l.Module.CourseId, ModuleOrder = l.Module.OrderIndex })
            .FirstOrDefaultAsync();
        if (current is null) return;

        // 1. Same module, next order.
        var next = await _db.Lessons.AsNoTracking()
            .Where(l => l.ModuleId == current.ModuleId && l.OrderIndex > current.OrderIndex)
            .OrderBy(l => l.OrderIndex)
            .Select(l => new { l.Id })
            .FirstOrDefaultAsync();

        if (next is null)
        {
            // 2. First lesson of the next module in the same course.
            next = await _db.Lessons.AsNoTracking()
                .Where(l => l.Module.CourseId == current.CourseId && l.Module.OrderIndex > current.ModuleOrder)
                .OrderBy(l => l.Module.OrderIndex)
                .ThenBy(l => l.OrderIndex)
                .Select(l => new { l.Id })
                .FirstOrDefaultAsync();
        }
        if (next is null) return;

        // Idempotent grant — if a row already exists, just flip is_unlocked back to true.
        var existing = await _db.StudentLessonAccess
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.LessonId == next.Id);
        if (existing is null)
        {
            _db.StudentLessonAccess.Add(new StudentLessonAccess
            {
                StudentId = studentId,
                LessonId = next.Id,
                IsUnlocked = true,
                CreatedBy = null, // null = auto-unlocked by system, not a specific teacher
            });
        }
        else if (!existing.IsUnlocked)
        {
            existing.IsUnlocked = true;
            existing.UnlockedAt = DateTime.UtcNow;
        }
        else
        {
            // Already unlocked — no-op.
            return;
        }
        await _db.SaveChangesAsync();

        // Best-effort student notification — failure shouldn't roll back the completion.
        try
        {
            var title = await _db.Lessons.AsNoTracking()
                .Where(l => l.Id == next.Id)
                .Select(l => l.Title)
                .FirstOrDefaultAsync();
            await _notify.NotifyAsync(
                studentId,
                "lesson_unlocked",
                "New lesson unlocked!",
                title is null ? null : $"\"{title}\" is now available.",
                null);
        }
        catch { /* notification is best-effort */ }
    }

    public async Task<UnlockStatusResponse> GetUnlockStatusAsync(Guid studentId, Guid lessonId)
    {
        var unlocked = await _db.StudentLessonAccess
            .AsNoTracking()
            .AnyAsync(a => a.StudentId == studentId && a.LessonId == lessonId && a.IsUnlocked);
        return new UnlockStatusResponse { IsUnlocked = unlocked };
    }

    // ---------------- Reporting ----------------

    public async Task<List<QuizResultResponse>> GetGroupResultsAsync(Guid groupId)
    {
        var studentIds = await _db.GroupStudents
            .Where(gs => gs.GroupId == groupId)
            .Select(gs => gs.StudentId)
            .ToListAsync();

        var rows = await _db.QuizResults
            .AsNoTracking()
            .Where(q => studentIds.Contains(q.UserId))
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
        return rows.Select(ToQuizResultResponse).ToList();
    }

    public async Task<List<QuizResultResponse>> GetStudentResultsAsync(Guid studentId)
    {
        var rows = await _db.QuizResults
            .AsNoTracking()
            .Where(q => q.UserId == studentId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
        return rows.Select(ToQuizResultResponse).ToList();
    }

    // ---------------- Helpers ----------------

    private static QuizResultResponse ToQuizResultResponse(QuizResult q) => new()
    {
        Id = q.Id,
        UserId = q.UserId,
        LessonId = q.LessonId,
        ContentId = q.ContentId,
        Score = q.Score,
        Passed = q.Passed,
        TotalQuestions = q.TotalQuestions,
        MistakesLog = q.MistakesLog?.RootElement.Clone(),
        Metadata = q.Metadata?.RootElement.Clone(),
        TeacherFeedback = q.TeacherFeedback,
        CompletedAt = q.CreatedAt,
    };
}
