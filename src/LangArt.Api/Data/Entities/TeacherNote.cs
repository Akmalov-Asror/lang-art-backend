namespace LangArt.Api.Data.Entities;

/// <summary>
/// Private note a teacher (or admin) writes about a student. Not visible to
/// the student; used to share observations across the teaching team.
/// Multiple notes per (student, author) are allowed.
/// </summary>
public class TeacherNote
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid AuthorId { get; set; }
    /// <summary>Short category — e.g. "observation", "warning", "praise".</summary>
    public string Kind { get; set; } = "observation";
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Profile Student { get; set; } = null!;
    public Profile Author { get; set; } = null!;
}
