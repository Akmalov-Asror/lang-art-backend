namespace LangArt.Api.Data.Entities;

public class Lesson
{
    public Guid Id { get; set; }
    public Guid ModuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }

    // Review workflow — admin flips IsReviewed=true once they've audited the
    // lesson's content (used to QA the LLM-seeded test-english Grammar courses).
    public bool IsReviewed { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedBy { get; set; }

    public Module Module { get; set; } = null!;
    public Profile? Reviewer { get; set; }
    public ICollection<LessonContent> Contents { get; set; } = new List<LessonContent>();
    public ICollection<LessonResource> Resources { get; set; } = new List<LessonResource>();
    public ICollection<LessonCompletion> Completions { get; set; } = new List<LessonCompletion>();
    public ICollection<QuizResult> QuizResults { get; set; } = new List<QuizResult>();
    public ICollection<StudentLessonAccess> StudentLessonAccesses { get; set; } = new List<StudentLessonAccess>();
}
