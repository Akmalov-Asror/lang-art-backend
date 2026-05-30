using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Parents.Dto;

public class ChildSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Relationship { get; set; }
    public int TotalXp { get; set; }
    public int Level { get; set; }
    public int CurrentStreak { get; set; }
    public int LessonsCompleted { get; set; }
    public int AttendancePresentCount { get; set; }
    public int AttendanceAbsentCount { get; set; }
    public int OverallSkill { get; set; }
    public DateTime? LastActivityDate { get; set; }
}

public class LinkChildRequest
{
    [Required]
    public Guid ChildId { get; set; }
    [MaxLength(50)]
    public string? Relationship { get; set; } = "guardian";
}

public class CreateParentAccountRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
    [Required, MinLength(1)]
    public string FullName { get; set; } = string.Empty;
    [Required]
    public Guid ChildId { get; set; }
    [MaxLength(50)]
    public string? Relationship { get; set; } = "guardian";
}
