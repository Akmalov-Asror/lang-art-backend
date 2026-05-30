using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Clubs.Dto;

public class ClubEventDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Kind { get; set; } = "club";
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int? Capacity { get; set; }
    public int LaDollarReward { get; set; }
    public Guid CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public bool IsCancelled { get; set; }
    public string? CoverImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RsvpCount { get; set; }
    public int AttendedCount { get; set; }
    public string? MyRsvpStatus { get; set; }
}

public class ClubEventRsvpDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Status { get; set; } = "going";
    public DateTime CreatedAt { get; set; }
}

public class CreateEventRequest
{
    [Required, MinLength(2)] public string Title { get; set; } = string.Empty;
    [Required] public string Kind { get; set; } = "club";
    public string? Description { get; set; }
    public string? Location { get; set; }
    [Required] public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int? Capacity { get; set; }
    public int LaDollarReward { get; set; } = 0;
    public Guid? BranchId { get; set; }
    public string? CoverImageUrl { get; set; }
}

public class UpdateEventRequest
{
    public string? Title { get; set; }
    public string? Kind { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int? Capacity { get; set; }
    public int? LaDollarReward { get; set; }
    public Guid? BranchId { get; set; }
    public bool? IsCancelled { get; set; }
    public string? CoverImageUrl { get; set; }
}

public class RsvpRequest
{
    [Required] public string Status { get; set; } = "going";    // going, maybe
}

public class MarkAttendanceRequest
{
    public Dictionary<Guid, string> Statuses { get; set; } = new();   // userId -> attended | no_show
}
