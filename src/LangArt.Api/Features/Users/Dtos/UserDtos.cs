using System.ComponentModel.DataAnnotations;
using LangArt.Api.Data.Enums;

namespace LangArt.Api.Features.Users.Dtos;

public class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(50)]
    public string Password { get; set; } = string.Empty;

    [Required, MinLength(2), MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public Role? Role { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateUserRequest
{
    [MinLength(2), MaxLength(100)]
    public string? FullName { get; set; }

    public Role? Role { get; set; }
    public bool? IsActive { get; set; }
}

public class UserStatsResponse
{
    public int TotalUsers { get; set; }
    public int TotalStudents { get; set; }
    public int TotalTeachers { get; set; }
    public int TotalCourses { get; set; }
}
