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

    public ProgressService(AppDbContext db, Features.Notifications.NotificationsService notify)
    {
        _db = db;
        _notify = notify;
    }

    // ---------------- Completions ----------------

    public async Task<LessonCompletionResponse> MarkCompleteAsync(Guid userId, Guid lessonId)
    {
        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == lessonId);
        if (!lessonExists) throw new NotFoundException("Lesson not found");

        var existing = await _db.LessonCompletions.FirstOrDefaultAsync(c => c.UserId == userId && c.LessonId == lessonId);
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
