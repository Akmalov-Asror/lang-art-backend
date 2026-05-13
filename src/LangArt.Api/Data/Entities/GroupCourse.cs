namespace LangArt.Api.Data.Entities;

public class GroupCourse
{
    public Guid GroupId { get; set; }
    public Guid CourseId { get; set; }
    public DateTime AssignedAt { get; set; }

    public Group Group { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
