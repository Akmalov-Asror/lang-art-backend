using LangArt.Api.Data;
using LangArt.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Gamification;

/// <summary>
/// Stateless rule engine that decides whether a user has met a given badge's
/// criteria. Today the rules are hard-coded against badge <c>Code</c>; the
/// <c>badges.criteria</c> jsonb column exists so this can become data-driven
/// later (e.g. <c>{ "kind": "streak", "days": 7 }</c>) without changing the
/// callers.
/// </summary>
public static class BadgeCriteriaEvaluator
{
    public static async Task<bool> IsMetAsync(string badgeCode, Guid userId, AppDbContext db, CancellationToken ct)
    {
        switch (badgeCode)
        {
            case "first_lesson":
                return await db.LessonCompletions.AnyAsync(c => c.UserId == userId, ct);

            case "streak_7":
                return await StreakAtLeast(userId, 7, db, ct);
            case "streak_30":
                return await StreakAtLeast(userId, 30, db, ct);
            case "streak_100":
                return await StreakAtLeast(userId, 100, db, ct);

            case "quiz_master_10":
                return await db.QuizResults.CountAsync(q => q.UserId == userId && q.Passed, ct) >= 10;
            case "quiz_master_50":
                return await db.QuizResults.CountAsync(q => q.UserId == userId && q.Passed, ct) >= 50;
            case "quiz_perfect_10":
                return await db.QuizResults
                    .Where(q => q.UserId == userId && q.TotalQuestions > 0 && q.Score == q.TotalQuestions)
                    .CountAsync(ct) >= 10;

            case "polyglot":
                // 2+ distinct courses the user has at least one completion in.
                return await db.LessonCompletions
                    .Where(c => c.UserId == userId)
                    .Select(c => c.Lesson.Module.CourseId)
                    .Distinct()
                    .CountAsync(ct) >= 2;

            case "early_bird":
                return await db.LessonCompletions
                    .AnyAsync(c => c.UserId == userId && c.CompletedAt.Hour < 8, ct);
            case "night_owl":
                return await db.LessonCompletions
                    .AnyAsync(c => c.UserId == userId && c.CompletedAt.Hour >= 23, ct);

            default:
                // Unknown code: treat as never-earned. Keeps the system safe to ship
                // new badge codes without crashing existing evaluations.
                return false;
        }
    }

    private static async Task<bool> StreakAtLeast(Guid userId, int days, AppDbContext db, CancellationToken ct)
    {
        var streak = await db.UserStreaks.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (streak is null) return false;
        return streak.LongestStreak >= days;
    }
}
