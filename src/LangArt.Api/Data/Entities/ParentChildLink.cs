namespace LangArt.Api.Data.Entities;

/// <summary>
/// Links a parent (role="parent") to one or more student profiles. A student
/// can have multiple parents; a parent can have multiple children. Used to
/// scope the parent portal: parents only see progress for their linked children.
/// </summary>
public class ParentChildLink
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    public Guid ChildId { get; set; }
    public string? Relationship { get; set; } // "mother", "father", "guardian"
    public DateTime CreatedAt { get; set; }

    public Profile Parent { get; set; } = null!;
    public Profile Child { get; set; } = null!;
}
