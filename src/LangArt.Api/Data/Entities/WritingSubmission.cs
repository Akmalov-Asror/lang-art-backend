namespace LangArt.Api.Data.Entities;

/// <summary>
/// One row per student writing attempt. Mirrors <see cref="SpeakingSubmission"/>:
///   submitted → ai_graded → (optionally) teacher_reviewed
/// AI grade fields are filled by <see cref="Features.Writing.IAiWritingGradingService"/>
/// (Speaking uses Whisper + GPT; writing skips transcription and feeds the
/// student text directly into GPT against an IELTS-style rubric).
/// </summary>
public class WritingSubmission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
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
