namespace LangArt.Api.Data.Entities;

/// <summary>
/// CRM lead — a prospective student who's expressed interest but isn't enrolled yet.
/// Moves through stages: new → contacted → trial_scheduled → converted | lost.
/// Optionally promoted to a real <see cref="Profile"/> when converted.
/// </summary>
public class Lead
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Source { get; set; } = "other";          // walk-in, instagram, referral, website, ad, other
    public string Stage { get; set; } = "new";              // new, contacted, trial_scheduled, converted, lost
    public string? InterestLevel { get; set; }              // A1..C2
    public string? Notes { get; set; }
    public Guid? AssignedTo { get; set; }                   // admin/manager Profile.Id
    public Guid? ConvertedProfileId { get; set; }           // set when stage = converted
    public DateTime? TrialAt { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
    public string? LostReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<LeadActivity> Activities { get; set; } = new List<LeadActivity>();
}

public class LeadActivity
{
    public Guid Id { get; set; }
    public Guid LeadId { get; set; }
    public Guid ActorId { get; set; }                       // admin who logged this
    public string Kind { get; set; } = "note";              // note, call, message, trial, stage_change, follow_up
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Lead Lead { get; set; } = null!;
}
