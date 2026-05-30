using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Branches.Dto;

public class BranchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? ManagerName { get; set; }
    public Guid? ManagerProfileId { get; set; }
    public bool IsActive { get; set; }
    public int StudentCount { get; set; }
    public int TeacherCount { get; set; }
    public int GroupCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBranchRequest
{
    [Required, MinLength(2)] public string Name { get; set; } = string.Empty;
    [Required, MinLength(2)] public string Code { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? ManagerName { get; set; }
    public Guid? ManagerProfileId { get; set; }
}

public class UpdateBranchRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? ManagerName { get; set; }
    public Guid? ManagerProfileId { get; set; }
    public bool? IsActive { get; set; }
}

public class AssignBranchRequest
{
    public Guid? BranchId { get; set; }   // null = unassign
}
