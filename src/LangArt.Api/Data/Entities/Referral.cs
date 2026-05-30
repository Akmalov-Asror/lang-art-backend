namespace LangArt.Api.Data.Entities;

/// <summary>
/// Tracks a single referral. When the referrer's code is used on signup or
/// attached to a lead that later converts, an entry is created here and an
/// LA Dollar reward is paid to the referrer.
/// </summary>
public class Referral
{
    public Guid Id { get; set; }
    public Guid ReferrerId { get; set; }
    public Guid? RefereeProfileId { get; set; }          // populated once signup happens
    public Guid? LeadId { get; set; }                    // populated if seeded from a lead
    public string Status { get; set; } = "pending";      // pending, converted, rewarded, expired
    public int RewardLaDollars { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public DateTime? RewardedAt { get; set; }
}
