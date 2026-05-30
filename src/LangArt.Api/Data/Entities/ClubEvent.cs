namespace LangArt.Api.Data.Entities;

/// <summary>
/// Extra-curricular event: a club meetup, workshop, debate, museum visit, etc.
/// Students RSVP and attendance is logged at event time. Optional LA Dollar reward on attend.
/// </summary>
public class ClubEvent
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Kind { get; set; } = "club";          // club, workshop, debate, visit, social
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int? Capacity { get; set; }                   // null = unlimited
    public int LaDollarReward { get; set; }              // awarded on attendance
    public Guid CreatedBy { get; set; }
    public Guid? BranchId { get; set; }
    public bool IsCancelled { get; set; }
    public string? CoverImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ClubEventRsvp> Rsvps { get; set; } = new List<ClubEventRsvp>();
}

public class ClubEventRsvp
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = "going";        // going, maybe, attended, no_show
    public DateTime CreatedAt { get; set; }

    public ClubEvent Event { get; set; } = null!;
}
