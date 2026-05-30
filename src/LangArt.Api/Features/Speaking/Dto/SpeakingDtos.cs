using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Speaking.Dto;

public class SpeakingSubmissionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid ContentId { get; set; }
    public string AudioUrl { get; set; } = string.Empty;
    public string? Transcript { get; set; }

    public int? AiGradePronunciation { get; set; }
    public int? AiGradeFluency { get; set; }
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

public class SubmitSpeakingRequest
{
    [Required]
    public Guid ContentId { get; set; }

    [Required]
    public string AudioUrl { get; set; } = string.Empty;
}

public class TeacherReviewRequest
{
    [Required, Range(0, 100)]
    public int Grade { get; set; }

    [Required]
    public string Feedback { get; set; } = string.Empty;
}

public class AiGradeResult
{
    public string Transcript { get; set; } = string.Empty;
    public int Pronunciation { get; set; }
    public int Fluency { get; set; }
    public int Grammar { get; set; }
    public int Vocabulary { get; set; }
    public int Total { get; set; }
    public string Feedback { get; set; } = string.Empty;
}
