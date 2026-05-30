namespace LangArt.Api.Data.Entities;

/// <summary>
/// A physical or logical branch of the school. Multi-tenant-lite: every
/// student/teacher/group can be tagged with a branch_id (nullable). Filtering
/// by branch_id lets head office see one branch at a time while still
/// holding all data in one database.
/// </summary>
public class Branch
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;     // short slug, e.g. "tashkent-yunusabad"
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? ManagerName { get; set; }
    public Guid? ManagerProfileId { get; set; }          // optional admin owner
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
