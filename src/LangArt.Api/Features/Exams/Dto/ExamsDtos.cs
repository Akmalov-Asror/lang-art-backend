using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace LangArt.Api.Features.Exams.Dto;

public class ExamDto
{
    public Guid Id { get; set; }
    public Guid? CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string KindLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonElement Payload { get; set; }
    public int PassingScore { get; set; }
    public int? TimeLimitSeconds { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int AttemptsByMe { get; set; }
    public int? BestScoreByMe { get; set; }
    public bool? BestPassedByMe { get; set; }
}

public class ExamAttemptDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public Guid ExamId { get; set; }
    public string? ExamTitle { get; set; }
    public string? ExamKind { get; set; }
    public int Score { get; set; }
    public int OutOf { get; set; }
    public int ScorePct { get; set; }
    public bool Passed { get; set; }
    public JsonElement? SectionScores { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CreateExamRequest
{
    public Guid? CourseId { get; set; }

    [Required, RegularExpression("^(unit_review|progress_exam|midterm|final|placement_ket|placement_pet|placement_fce|placement_cae|placement_cpe)$")]
    public string Kind { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public JsonElement Payload { get; set; }

    [Range(0, 100)]
    public int PassingScore { get; set; } = 70;

    public int? TimeLimitSeconds { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateExamRequest : CreateExamRequest { }

public class SubmitExamRequest
{
    /// <summary>Per-section { reading: { q1: 0, q2: 1 }, listening: {...}, etc. }</summary>
    [Required]
    public JsonElement Answers { get; set; }
}
