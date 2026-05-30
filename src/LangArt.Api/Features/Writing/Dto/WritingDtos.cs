using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Writing.Dto;

public class WritingSubmissionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid ContentId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int WordCount { get; set; }

    public int? AiGradeTaskAchievement { get; set; }
    public int? AiGradeCoherence { get; set; }
    public int? AiGradeGrammar { get; set; }
    public int? AiGradeVocabulary { get; set; }
    public int? AiTotal { get; set; }
    public string? AiFeedback { get; set; }
    public DateTime? AiGradedAt { get; set; }

    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public int? TeacherGradeTotal { get; set; }
    public string? TeacherFeedback { get; set; }
    public DateTime? TeacherReviewedAt { get; set; }

    public int? FinalGrade { get; set; }
    public string Status { get; set; } = "submitted";
    public DateTime CreatedAt { get; set; }
}

public class SubmitWritingRequest
{
    [Required]
    public Guid ContentId { get; set; }

    [Required, MinLength(10)]
    public string Text { get; set; } = string.Empty;
}

public class TeacherWritingReviewRequest
{
    [Required, Range(0, 100)]
    public int Grade { get; set; }

    [Required]
    public string Feedback { get; set; } = string.Empty;
}

public class AiWritingGradeResult
{
    public int TaskAchievement { get; set; }
    public int Coherence { get; set; }
    public int Grammar { get; set; }
    public int Vocabulary { get; set; }
    public int Total { get; set; }
    public string Feedback { get; set; } = string.Empty;
}
