using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Referrals.Dto;

public class MyReferralStatusDto
{
    public string Code { get; set; } = string.Empty;
    public int TotalReferred { get; set; }
    public int Converted { get; set; }
    public int Rewarded { get; set; }
    public int TotalEarnedLaDollars { get; set; }
    public List<ReferralEntryDto> Recent { get; set; } = new();
}

public class ReferralEntryDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "pending";
    public string? RefereeName { get; set; }
    public string? LeadName { get; set; }
    public int RewardLaDollars { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RewardedAt { get; set; }
}

public class AttachReferralRequest
{
    [Required] public string Code { get; set; } = string.Empty;
    public int RewardLaDollars { get; set; } = 50;
}

public class RewardReferralRequest
{
    public int? RewardLaDollars { get; set; }
}

public class AlumniDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SetAlumniRequest
{
    public bool IsAlumni { get; set; }
    public string? Note { get; set; }
}
