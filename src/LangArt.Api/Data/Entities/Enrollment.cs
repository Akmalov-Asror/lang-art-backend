namespace LangArt.Api.Data.Entities;

public class Enrollment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public DateTime EnrolledAt { get; set; }

    public Profile User { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
