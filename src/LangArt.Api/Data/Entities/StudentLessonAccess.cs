namespace LangArt.Api.Data.Entities;

public class StudentLessonAccess
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid LessonId { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime UnlockedAt { get; set; }
    public Guid? CreatedBy { get; set; }

    public Profile Student { get; set; } = null!;
    public Lesson Lesson { get; set; } = null!;
    public Profile? Creator { get; set; }
}
