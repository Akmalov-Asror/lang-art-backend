using System.Text.Json;

namespace LangArt.Api.Data.Entities;

/// <summary>
/// A structured assessment — Unit Review / Progress Exam / Midterm / Final /
/// Placement. Owned by a course (or null for system-wide placement). The
/// actual sections (reading + listening + writing + speaking + grammar) live
/// in the <see cref="ContentPayload"/> JSON so an exam is conceptually a
/// "test paper" with multiple sub-parts.
/// </summary>
public class Exam
{
    public Guid Id { get; set; }
    public Guid? CourseId { get; set; }
    /// <summary>One of: unit_review, progress_exam, midterm, final, placement_ket, placement_pet, placement_fce, placement_cae, placement_cpe.</summary>
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>JSON: { sections: [{ kind, title, questions[]/passage/prompt/etc }], passing_score, time_limit_seconds }.</summary>
    public JsonDocument Payload { get; set; } = null!;
    public int PassingScore { get; set; } = 70;
    public int? TimeLimitSeconds { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Course? Course { get; set; }
    public ICollection<ExamAttempt> Attempts { get; set; } = new List<ExamAttempt>();
}

public class ExamAttempt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ExamId { get; set; }
    public int Score { get; set; }
    public int OutOf { get; set; }
    public bool Passed { get; set; }
    /// <summary>JSON: per-section scores { reading: 8, listening: 7, writing: 75, speaking: 80 }.</summary>
    public JsonDocument? SectionScores { get; set; }
    /// <summary>Raw answers — for review and analytics.</summary>
    public JsonDocument? Answers { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Profile User { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
}
