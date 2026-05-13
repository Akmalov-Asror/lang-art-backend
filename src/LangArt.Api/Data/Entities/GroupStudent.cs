namespace LangArt.Api.Data.Entities;

public class GroupStudent
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime JoinedAt { get; set; }

    public Group Group { get; set; } = null!;
    public Profile Student { get; set; } = null!;
}
