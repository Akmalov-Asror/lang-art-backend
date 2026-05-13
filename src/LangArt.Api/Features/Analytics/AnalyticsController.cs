using System.Text.Json;
using LangArt.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Analytics;

[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "admin,teacher")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AnalyticsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Aggregates the per-question wrong-answer counts for a single quiz content block.
    /// Reads each <c>quiz_results.mistakes_log</c> entry — which the frontend submits as
    /// <c>{ "questionId": "selectedIndex", ... }</c> — and rolls them up.
    /// </summary>
    [HttpGet("content/{contentId:guid}/quiz")]
    public async Task<QuizAnalyticsResponse> ContentQuizAnalytics(Guid contentId)
    {
        var content = await _db.LessonContent
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contentId)
            ?? throw new Common.Exceptions.NotFoundException("Content not found");

        var results = await _db.QuizResults
            .AsNoTracking()
            .Where(q => q.ContentId == contentId)
            .ToListAsync();

        // Pull the questions array out of the content_payload so we can label them.
        var labels = new Dictionary<string, string>();
        try
        {
            if (content.ContentPayload.RootElement.TryGetProperty("questions", out var qs) && qs.ValueKind == JsonValueKind.Array)
            {
                foreach (var q in qs.EnumerateArray())
                {
                    if (q.TryGetProperty("id", out var id) && q.TryGetProperty("question", out var text))
                    {
                        labels[id.GetString() ?? ""] = text.GetString() ?? "";
                    }
                }
            }
        }
        catch { /* malformed payload — fall through with empty labels */ }

        // Count mistakes per question across all attempts.
        var mistakes = new Dictionary<string, int>();
        foreach (var r in results)
        {
            if (r.MistakesLog is null) continue;
            try
            {
                foreach (var prop in r.MistakesLog.RootElement.EnumerateObject())
                {
                    mistakes.TryGetValue(prop.Name, out var c);
                    mistakes[prop.Name] = c + 1;
                }
            }
            catch { /* skip malformed entries */ }
        }

        var totalAttempts = results.Count;
        IEnumerable<string> questionIds = labels.Count > 0 ? labels.Keys : mistakes.Keys;
        var byQuestion = questionIds
            .Select(qid => new QuestionStat
            {
                QuestionId = qid,
                QuestionText = labels.GetValueOrDefault(qid, qid),
                WrongCount = mistakes.GetValueOrDefault(qid, 0),
                AttemptCount = totalAttempts,
                WrongPct = totalAttempts == 0 ? 0 : (int)Math.Round(100.0 * mistakes.GetValueOrDefault(qid, 0) / totalAttempts),
            })
            .OrderByDescending(s => s.WrongCount)
            .ToList();

        return new QuizAnalyticsResponse
        {
            ContentId = contentId,
            TotalAttempts = totalAttempts,
            AverageScorePct = totalAttempts == 0
                ? 0
                : (int)Math.Round(results.Where(r => r.TotalQuestions > 0).DefaultIfEmpty().Average(r => r == null ? 0 : 100.0 * r.Score / Math.Max(1, r.TotalQuestions))),
            PassRatePct = totalAttempts == 0 ? 0 : (int)Math.Round(100.0 * results.Count(r => r.Passed) / totalAttempts),
            Questions = byQuestion,
        };
    }
}

public class QuizAnalyticsResponse
{
    public Guid ContentId { get; set; }
    public int TotalAttempts { get; set; }
    public int AverageScorePct { get; set; }
    public int PassRatePct { get; set; }
    public List<QuestionStat> Questions { get; set; } = new();
}

public class QuestionStat
{
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public int WrongCount { get; set; }
    public int AttemptCount { get; set; }
    public int WrongPct { get; set; }
}
