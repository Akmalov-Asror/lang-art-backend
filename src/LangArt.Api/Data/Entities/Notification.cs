namespace LangArt.Api.Data.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Free-form discriminator: "quiz_graded", "attendance_marked", "course_assigned", "lesson_unlocked".</summary>
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Profile User { get; set; } = null!;
}
