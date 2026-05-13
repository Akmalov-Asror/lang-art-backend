using System.ComponentModel.DataAnnotations;
using LangArt.Api.Data.Enums;

namespace LangArt.Api.Features.Classroom.Dtos;

// ---------------- Group responses ----------------

public class TeacherSummary
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class GroupStudentRowResponse
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class GroupResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TeacherId { get; set; }
    public string? ScheduleInfo { get; set; }
    public string[] ScheduleDays { get; set; } = Array.Empty<string>();
    public string? StartTime { get; set; }   // "HH:mm" or null
    public string? EndTime { get; set; }
    public DateOnly? StartDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public TeacherSummary? Teacher { get; set; }
    public List<GroupStudentRowResponse> GroupStudents { get; set; } = new();
}

// ---------------- Group requests ----------------

public class CreateGroupRequest
{
    [Required, MinLength(1), MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid TeacherId { get; set; }

    public string? ScheduleInfo { get; set; }
    public string[]? ScheduleDays { get; set; }
    public string? StartTime { get; set; }   // "HH:mm"
    public string? EndTime { get; set; }
    public DateOnly? StartDate { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateGroupRequest
{
    public string? Name { get; set; }
    public Guid? TeacherId { get; set; }
    public string? ScheduleInfo { get; set; }
    public string[]? ScheduleDays { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public DateOnly? StartDate { get; set; }
    public bool? IsActive { get; set; }
}

// ---------------- Group students ----------------

public class ProfileSummary
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GroupStudentWithProfileResponse
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime JoinedAt { get; set; }
    public ProfileSummary Student { get; set; } = new();
}

// ---------------- Group courses ----------------

public class CourseSummary
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
}

public class GroupCourseResponse
{
    public Guid GroupId { get; set; }
    public Guid CourseId { get; set; }
    public DateTime AssignedAt { get; set; }
    public CourseSummary? Course { get; set; }
}

// ---------------- Attendance ----------------

public class AttendanceResponse
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid StudentId { get; set; }
    public DateOnly Date { get; set; }
    public string Status { get; set; } = "present";
    public string? Notes { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MarkAttendanceRequest
{
    [Required]
    public Guid GroupId { get; set; }

    [Required]
    public Guid StudentId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public AttendanceStatus Status { get; set; }

    public string? Notes { get; set; }
}

public class BatchAttendanceRequest
{
    [Required, MinLength(1)]
    public List<MarkAttendanceRequest> Records { get; set; } = new();
}
