using System.Text.Json;
using LangArt.Api.Data.Enums;

namespace LangArt.Api.Data.Entities;

public class LessonContent
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public ContentType Type { get; set; }
    public JsonDocument ContentPayload { get; set; } = null!;
    public int OrderIndex { get; set; }
    public string? ExerciseType { get; set; }
    public DateTime CreatedAt { get; set; }

    public Lesson Lesson { get; set; } = null!;
    public ICollection<QuizResult> QuizResults { get; set; } = new List<QuizResult>();
}
