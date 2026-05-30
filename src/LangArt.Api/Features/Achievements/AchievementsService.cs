using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Achievements.Dto;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LangArt.Api.Features.Achievements;

public class AchievementsService
{
    public static readonly (string Code, string Label, string Emoji)[] Categories = new[]
    {
        ("student_of_month",  "Student of the Month", "🏆"),
        ("best_speaker",      "Best Speaker",          "🎤"),
        ("best_writer",       "Best Writer",           "✏️"),
        ("best_reader",       "Best Reader",           "📖"),
        ("best_listener",     "Best Listener",         "🎧"),
        ("best_grammarian",   "Best Grammarian",       "📐"),
        ("best_vocabularist", "Best Vocabularist",     "📚"),
    };

    private readonly AppDbContext _db;
    private readonly ILogger<AchievementsService> _logger;

    public AchievementsService(AppDbContext db, ILogger<AchievementsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AchievementDto>> ListAllAsync(CancellationToken ct)
    {
        var rows = await _db.Achievements.AsNoTracking()
            .OrderByDescending(a => a.PeriodYear)
            .ThenByDescending(a => a.PeriodMonth)
            .ThenBy(a => a.Category)
            .Include(a => a.User)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AchievementDto>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        var rows = await _db.Achievements.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.PeriodYear)
            .ThenByDescending(a => a.PeriodMonth)
            .Include(a => a.User)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AchievementMonthlyDto>> ListGroupedByMonthAsync(CancellationToken ct)
    {
        var all = await ListAllAsync(ct);
        return all
            .GroupBy(a => new { a.PeriodYear, a.PeriodMonth })
            .Select(g => new AchievementMonthlyDto
            {
                Year = g.Key.PeriodYear,
                Month = g.Key.PeriodMonth,
                Label = new DateTime(g.Key.PeriodYear, g.Key.PeriodMonth, 1).ToString("MMMM yyyy"),
                Awards = g.OrderBy(a => a.Category).ToList(),
            })
            .OrderByDescending(g => g.Year)
            .ThenByDescending(g => g.Month)
            .ToList();
    }

    /// <summary>
    /// Computes winners for the previous (or specified) month and inserts
    /// Achievement rows. Idempotent: the unique index on
    /// (category, period_year, period_month) prevents duplicates if re-run.
    /// </summary>
    public async Task<ComputeAwardsResult> ComputeAwardsAsync(int year, int month, CancellationToken ct)
    {
        var periodStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        var awards = new List<Achievement>();

        // ---- Per-skill winners (averaged final_grade for that month) ----
        var speakingWinner = await BestByAverage(
            _db.SpeakingSubmissions.AsNoTracking()
                .Where(s => s.CreatedAt >= periodStart && s.CreatedAt < periodEnd && s.FinalGrade != null)
                .Select(s => new { s.UserId, Score = s.FinalGrade!.Value }),
            ct);
        if (speakingWinner is not null)
            awards.Add(new Achievement { UserId = speakingWinner.Value.UserId, Category = "best_speaker", PeriodYear = year, PeriodMonth = month, Score = speakingWinner.Value.Score });

        var writingWinner = await BestByAverage(
            _db.WritingSubmissions.AsNoTracking()
                .Where(s => s.CreatedAt >= periodStart && s.CreatedAt < periodEnd && s.FinalGrade != null)
                .Select(s => new { s.UserId, Score = s.FinalGrade!.Value }),
            ct);
        if (writingWinner is not null)
            awards.Add(new Achievement { UserId = writingWinner.Value.UserId, Category = "best_writer", PeriodYear = year, PeriodMonth = month, Score = writingWinner.Value.Score });

        // Reading + Listening + Grammar from QuizResult, partitioned by exercise_type.
        var quizMonthly = await _db.QuizResults.AsNoTracking()
            .Where(q => q.CreatedAt >= periodStart && q.CreatedAt < periodEnd && q.TotalQuestions > 0)
            .Select(q => new
            {
                q.UserId,
                Type = q.Content!.ExerciseType ?? "quiz",
                Pct = (int)Math.Round((double)q.Score / q.TotalQuestions * 100.0),
            })
            .ToListAsync(ct);

        AddWinnerFromList(awards, quizMonthly, t => t == "reading", "best_reader", year, month);
        AddWinnerFromList(awards, quizMonthly, t => t == "listening", "best_listener", year, month);
        AddWinnerFromList(awards, quizMonthly, t => t == "quiz" || t == "fill_blank", "best_grammarian", year, month);

        // Vocabulary — most words mastered this month.
        var vocabMonthly = await _db.UserWordlistEntries.AsNoTracking()
            .Where(e => e.Status == "learned"
                     && e.LastReviewedAt != null
                     && e.LastReviewedAt >= periodStart
                     && e.LastReviewedAt < periodEnd)
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var vocabWinner = vocabMonthly.OrderByDescending(v => v.Count).FirstOrDefault();
        if (vocabWinner is not null)
            awards.Add(new Achievement { UserId = vocabWinner.UserId, Category = "best_vocabularist", PeriodYear = year, PeriodMonth = month, Score = vocabWinner.Count });

        // Student of the Month — highest XP earned this month.
        var xpMonthly = await _db.XpLedger.AsNoTracking()
            .Where(l => l.CreatedAtUtc >= periodStart && l.CreatedAtUtc < periodEnd)
            .GroupBy(l => l.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);
        var somWinner = xpMonthly.OrderByDescending(v => v.Total).FirstOrDefault();
        if (somWinner is not null)
            awards.Add(new Achievement { UserId = somWinner.UserId, Category = "student_of_month", PeriodYear = year, PeriodMonth = month, Score = somWinner.Total });

        // Insert; the unique partial index handles idempotency.
        int added = 0;
        foreach (var a in awards)
        {
            _db.Achievements.Add(a);
            try
            {
                await _db.SaveChangesAsync(ct);
                added++;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
            {
                // Already exists — detach and continue.
                foreach (var entry in _db.ChangeTracker.Entries<Achievement>().Where(e => e.State == EntityState.Added).ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        var inserted = await _db.Achievements.AsNoTracking()
            .Where(a => a.PeriodYear == year && a.PeriodMonth == month)
            .Include(a => a.User)
            .ToListAsync(ct);

        return new ComputeAwardsResult
        {
            Year = year,
            Month = month,
            NewAwardsCount = added,
            Awards = inserted.Select(ToDto).ToList(),
        };
    }

    private static void AddWinnerFromList(
        List<Achievement> awards,
        IEnumerable<dynamic> rows,
        Func<string, bool> typeFilter,
        string category,
        int year,
        int month)
    {
        var filtered = rows.Where(r => typeFilter((string)r.Type)).ToList();
        if (filtered.Count == 0) return;
        var best = filtered
            .GroupBy(r => (Guid)r.UserId)
            .Select(g => new { UserId = g.Key, Avg = g.Average(x => (double)(int)x.Pct) })
            .OrderByDescending(x => x.Avg)
            .First();
        awards.Add(new Achievement
        {
            UserId = best.UserId,
            Category = category,
            PeriodYear = year,
            PeriodMonth = month,
            Score = (int)Math.Round(best.Avg),
        });
    }

    private static async Task<(Guid UserId, int Score)?> BestByAverage(
        IQueryable<dynamic> q, CancellationToken ct)
    {
        var rows = await q.ToListAsync(ct);
        if (rows.Count == 0) return null;
        var best = rows
            .GroupBy(r => (Guid)r.UserId)
            .Select(g => new { UserId = g.Key, Avg = g.Average(x => (double)(int)x.Score) })
            .OrderByDescending(x => x.Avg)
            .First();
        return (best.UserId, (int)Math.Round(best.Avg));
    }

    private static AchievementDto ToDto(Achievement a)
    {
        var cat = Categories.FirstOrDefault(c => c.Code == a.Category);
        return new AchievementDto
        {
            Id = a.Id,
            UserId = a.UserId,
            UserName = a.User?.FullName ?? string.Empty,
            AvatarUrl = a.User?.AvatarUrl,
            Category = a.Category,
            CategoryLabel = cat.Label ?? a.Category,
            CategoryEmoji = cat.Emoji ?? "🏅",
            PeriodYear = a.PeriodYear,
            PeriodMonth = a.PeriodMonth,
            Score = a.Score,
            AwardedAt = a.AwardedAt,
        };
    }
}
