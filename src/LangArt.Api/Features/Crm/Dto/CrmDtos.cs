using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Crm.Dto;

public class LeadDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Source { get; set; } = "other";
    public string Stage { get; set; } = "new";
    public string? InterestLevel { get; set; }
    public string? Notes { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public Guid? ConvertedProfileId { get; set; }
    public DateTime? TrialAt { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
    public string? LostReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ActivityCount { get; set; }
}

public class LeadActivityDto
{
    public Guid Id { get; set; }
    public Guid LeadId { get; set; }
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Kind { get; set; } = "note";
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateLeadRequest
{
    [Required, MinLength(2)]
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Source { get; set; } = "other";
    public string? InterestLevel { get; set; }
    public string? Notes { get; set; }
    public Guid? AssignedTo { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
}

public class UpdateLeadRequest
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Source { get; set; }
    public string? InterestLevel { get; set; }
    public string? Notes { get; set; }
    public Guid? AssignedTo { get; set; }
    public DateTime? TrialAt { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
}

public class ChangeStageRequest
{
    [Required]
    public string Stage { get; set; } = string.Empty;
    public string? LostReason { get; set; }
}

public class AddActivityRequest
{
    [Required]
    public string Kind { get; set; } = "note";
    [Required, MinLength(1)]
    public string Body { get; set; } = string.Empty;
}

public class CrmStatsDto
{
    public int New { get; set; }
    public int Contacted { get; set; }
    public int TrialScheduled { get; set; }
    public int Converted { get; set; }
    public int Lost { get; set; }
    public int Total { get; set; }
    public int ConversionPct { get; set; }
    public int OverdueFollowUps { get; set; }
}
