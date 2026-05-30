namespace LangArt.Api.Data.Entities;

/// <summary>
/// One row per student speaking attempt. The flow is:
///   submitted → ai_graded → (optionally) teacher_reviewed
/// AI grade fields are filled by <see cref="Features.Speaking.IAiSpeechGradingService"/>.
/// Teacher fields are filled by an explicit override.
/// </summary>
public class SpeakingSubmission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
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
    public int? TeacherGradeTotal { get; set; }
    public string? TeacherFeedback { get; set; }
    public DateTime? TeacherReviewedAt { get; set; }

    public int? FinalGrade { get; set; }
    public string Status { get; set; } = "submitted";
    public DateTime CreatedAt { get; set; }

    public Profile User { get; set; } = null!;
    public LessonContent Content { get; set; } = null!;
    public Profile? Teacher { get; set; }
}
