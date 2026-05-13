namespace LangArt.Api.Data.Entities;

public class LessonResource
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Lesson Lesson { get; set; } = null!;
}
