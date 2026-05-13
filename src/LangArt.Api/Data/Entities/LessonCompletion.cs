namespace LangArt.Api.Data.Entities;

public class LessonCompletion
{
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }
    public DateTime CompletedAt { get; set; }

    public Profile User { get; set; } = null!;
    public Lesson Lesson { get; set; } = null!;
}
