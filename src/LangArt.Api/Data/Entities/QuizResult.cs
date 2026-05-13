using System.Text.Json;

namespace LangArt.Api.Data.Entities;

public class QuizResult
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }
    public Guid? ContentId { get; set; }
    public int Score { get; set; }
    public bool Passed { get; set; }
    public int TotalQuestions { get; set; }
    public JsonDocument? MistakesLog { get; set; }
    public JsonDocument? Metadata { get; set; }
    public string? TeacherFeedback { get; set; }
    public DateTime CreatedAt { get; set; }

    public Profile User { get; set; } = null!;
    public Lesson Lesson { get; set; } = null!;
    public LessonContent? Content { get; set; }
}
