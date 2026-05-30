namespace LangArt.Api.Data.Entities;

/// <summary>
/// A student's personal collection of words they've added to "my wordlist".
/// Unique on (user_id, word_id) — the same word can only be added once.
/// <c>Status</c> tracks spaced-repetition state: new → learning → learned.
/// </summary>
public class UserWordlistEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WordId { get; set; }
    public string Status { get; set; } = "new";
    public DateTime? LastReviewedAt { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public DateTime AddedAt { get; set; }

    public int? DayNumber { get; set; }
    public string Source { get; set; } = "manual";
    public DateOnly? AssignedDate { get; set; }

    public Profile User { get; set; } = null!;
    public Word Word { get; set; } = null!;
}
