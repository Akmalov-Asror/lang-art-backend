namespace LangArt.Api.Data.Entities;

/// <summary>
/// A curated collection of words owned by a teacher or admin. <c>IsPublic</c>
/// wordlists are visible to every student; private ones only to the owner.
/// Students do NOT own wordlists — their personal collection is the
/// <see cref="UserWordlistEntry"/> rows pointing to individual <see cref="Word"/>s.
/// </summary>
public class Wordlist
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public string Level { get; set; } = "A1";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Profile Owner { get; set; } = null!;
    public ICollection<Word> Words { get; set; } = new List<Word>();
}
