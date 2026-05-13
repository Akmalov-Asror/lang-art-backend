namespace LangArt.Api.Data.Entities;

public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TeacherId { get; set; }
    public string? ScheduleInfo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly? StartDate { get; set; }
    public string[] ScheduleDays { get; set; } = Array.Empty<string>();
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public DateTime CreatedAt { get; set; }

    public Profile Teacher { get; set; } = null!;
    public ICollection<GroupStudent> GroupStudents { get; set; } = new List<GroupStudent>();
    public ICollection<GroupCourse> GroupCourses { get; set; } = new List<GroupCourse>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
