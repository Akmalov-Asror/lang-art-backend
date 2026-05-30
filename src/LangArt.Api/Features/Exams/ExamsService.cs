using System.Text.Json;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Exams.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Exams;

public class ExamsService
{
    public static readonly Dictionary<string, string> KindLabels = new()
    {
        { "unit_review",      "Unit Review" },
        { "progress_exam",    "Progress Exam" },
        { "midterm",          "Midterm" },
        { "final",            "Final Exam" },
        { "placement_ket",    "Cambridge KET (A2)" },
        { "placement_pet",    "Cambridge PET (B1)" },
        { "placement_fce",    "Cambridge FCE (B2)" },
        { "placement_cae",    "Cambridge CAE (C1)" },
        { "placement_cpe",    "Cambridge CPE (C2)" },
    };

    private readonly AppDbContext _db;

    public ExamsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ExamDto>> ListAsync(Guid currentUserId, CancellationToken ct)
    {
        var exams = await _db.Exams.AsNoTracking()
            .Where(e => e.IsActive)
            .Include(e => e.Course)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        // Per-user attempt aggregates.
        var examIds = exams.Select(e => e.Id).ToList();
        var attempts = await _db.ExamAttempts.AsNoTracking()
            .Where(a => a.UserId == currentUserId && examIds.Contains(a.ExamId))
            .ToListAsync(ct);
        var byExam = attempts.ToLookup(a => a.ExamId);

        return exams.Select(e => ToDto(e, byExam[e.Id].ToList())).ToList();
    }

    public async Task<ExamDto> GetByIdAsync(Guid examId, Guid currentUserId, CancellationToken ct)
    {
        var exam = await _db.Exams.AsNoTracking()
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == examId, ct)
            ?? throw new NotFoundException("Exam not found");
        var attempts = await _db.ExamAttempts.AsNoTracking()
            .Where(a => a.UserId == currentUserId && a.ExamId == examId)
            .ToListAsync(ct);
        return ToDto(exam, attempts);
    }

    public async Task<ExamDto> CreateAsync(CreateExamRequest req, CancellationToken ct)
    {
        var exam = new Exam
        {
            CourseId = req.CourseId,
            Kind = req.Kind,
            Title = req.Title,
            Description = req.Description,
            Payload = JsonDocument.Parse(req.Payload.GetRawText()),
            PassingScore = req.PassingScore,
            TimeLimitSeconds = req.TimeLimitSeconds,
            IsActive = req.IsActive,
        };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync(ct);
        var withCourse = await _db.Exams.AsNoTracking().Include(e => e.Course).FirstAsync(e => e.Id == exam.Id, ct);
        return ToDto(withCourse, new List<ExamAttempt>());
    }

    public async Task<ExamDto> UpdateAsync(Guid id, UpdateExamRequest req, CancellationToken ct)
    {
        var exam = await _db.Exams.Include(e => e.Course).FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Exam not found");

        exam.CourseId = req.CourseId;
        exam.Kind = req.Kind;
        exam.Title = req.Title;
        exam.Description = req.Description;
        exam.Payload = JsonDocument.Parse(req.Payload.GetRawText());
        exam.PassingScore = req.PassingScore;
        exam.TimeLimitSeconds = req.TimeLimitSeconds;
        exam.IsActive = req.IsActive;
        exam.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(exam, new List<ExamAttempt>());
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var deleted = await _db.Exams.Where(e => e.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new NotFoundException("Exam not found");
    }

    /// <summary>
    /// Submit a student attempt — auto-grades the multiple-choice sections
    /// against the exam's payload. Writing/speaking sections are recorded
    /// but not auto-graded (teacher reviews separately).
    /// </summary>
    public async Task<ExamAttemptDto> SubmitAsync(Guid userId, Guid examId, SubmitExamRequest req, CancellationToken ct)
    {
        var exam = await _db.Exams.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == examId, ct)
            ?? throw new NotFoundException("Exam not found");

        // Grade each MCQ section.
        var sectionScores = new Dictionary<string, int>();
        int totalScore = 0;
        int totalOutOf = 0;

        if (exam.Payload.RootElement.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array)
        {
            foreach (var section in sections.EnumerateArray())
            {
                if (!section.TryGetProperty("kind", out var kindEl)) continue;
                var sectionKind = kindEl.GetString() ?? "unknown";

                // Only auto-grade quiz-shaped sections (reading, listening, grammar, vocabulary).
                if (sectionKind is "writing" or "speaking")
                {
                    sectionScores[sectionKind] = -1; // Pending teacher review
                    continue;
                }

                if (!section.TryGetProperty("questions", out var qs) || qs.ValueKind != JsonValueKind.Array) continue;

                int sectionScore = 0;
                int sectionOutOf = 0;
                foreach (var q in qs.EnumerateArray())
                {
                    sectionOutOf++;
                    if (!q.TryGetProperty("id", out var qid)) continue;
                    if (!q.TryGetProperty("correct_answer_index", out var corr)) continue;
                    var qIdStr = qid.GetString() ?? "";

                    // Read student answer from req.Answers.<section>.<qid>
                    if (!req.Answers.TryGetProperty(sectionKind, out var secAns)) continue;
                    if (!secAns.TryGetProperty(qIdStr, out var studentAns)) continue;

                    var correctIdx = corr.GetInt32();
                    var studentIdx = studentAns.GetInt32();
                    if (correctIdx == studentIdx) sectionScore++;
                }
                sectionScores[sectionKind] = sectionOutOf == 0 ? 0 : (int)Math.Round(100.0 * sectionScore / sectionOutOf);
                totalScore += sectionScore;
                totalOutOf += sectionOutOf;
            }
        }

        int finalScorePct = totalOutOf == 0 ? 0 : (int)Math.Round(100.0 * totalScore / totalOutOf);
        bool passed = finalScorePct >= exam.PassingScore;

        var attempt = new ExamAttempt
        {
            UserId = userId,
            ExamId = examId,
            Score = totalScore,
            OutOf = totalOutOf,
            Passed = passed,
            SectionScores = JsonDocument.Parse(JsonSerializer.Serialize(sectionScores)),
            Answers = JsonDocument.Parse(req.Answers.GetRawText()),
            CompletedAt = DateTime.UtcNow,
        };
        _db.ExamAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        return new ExamAttemptDto
        {
            Id = attempt.Id,
            UserId = userId,
            ExamId = examId,
            ExamTitle = exam.Title,
            ExamKind = exam.Kind,
            Score = totalScore,
            OutOf = totalOutOf,
            ScorePct = finalScorePct,
            Passed = passed,
            SectionScores = attempt.SectionScores?.RootElement,
            StartedAt = attempt.StartedAt,
            CompletedAt = attempt.CompletedAt,
        };
    }

    public async Task<IReadOnlyList<ExamAttemptDto>> MyAttemptsAsync(Guid userId, CancellationToken ct)
    {
        var rows = await _db.ExamAttempts.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Exam)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(ct);

        return rows.Select(a => new ExamAttemptDto
        {
            Id = a.Id,
            UserId = a.UserId,
            ExamId = a.ExamId,
            ExamTitle = a.Exam.Title,
            ExamKind = a.Exam.Kind,
            Score = a.Score,
            OutOf = a.OutOf,
            ScorePct = a.OutOf == 0 ? 0 : (int)Math.Round(100.0 * a.Score / a.OutOf),
            Passed = a.Passed,
            SectionScores = a.SectionScores?.RootElement,
            StartedAt = a.StartedAt,
            CompletedAt = a.CompletedAt,
        }).ToList();
    }

    private static ExamDto ToDto(Exam e, List<ExamAttempt> myAttempts)
    {
        int? best = myAttempts.Count == 0
            ? null
            : myAttempts.Select(a => a.OutOf == 0 ? 0 : (int)Math.Round(100.0 * a.Score / a.OutOf)).Max();
        bool? bestPassed = myAttempts.Count == 0
            ? null
            : myAttempts.Any(a => a.Passed);

        return new ExamDto
        {
            Id = e.Id,
            CourseId = e.CourseId,
            CourseTitle = e.Course?.Title,
            Kind = e.Kind,
            KindLabel = KindLabels.GetValueOrDefault(e.Kind, e.Kind),
            Title = e.Title,
            Description = e.Description,
            Payload = e.Payload.RootElement,
            PassingScore = e.PassingScore,
            TimeLimitSeconds = e.TimeLimitSeconds,
            IsActive = e.IsActive,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            AttemptsByMe = myAttempts.Count,
            BestScoreByMe = best,
            BestPassedByMe = bestPassed,
        };
    }
}
