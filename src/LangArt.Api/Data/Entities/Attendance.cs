using LangArt.Api.Data.Enums;

namespace LangArt.Api.Data.Entities;

public class Attendance
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid StudentId { get; set; }
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Notes { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Group Group { get; set; } = null!;
    public Profile Student { get; set; } = null!;
    public Profile? Creator { get; set; }
}
