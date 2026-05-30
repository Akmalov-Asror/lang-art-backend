namespace LangArt.Api.Data.Entities;

/// <summary>
/// One row per user. The current running total — denormalised so leaderboards
/// don't have to sum the ledger on every read. <c>TotalXp</c> is always equal
/// to <c>SUM(xp_ledger.amount) WHERE user_id = …</c> for that user.
/// </summary>
public class UserXp
{
    public Guid UserId { get; set; }
    public int TotalXp { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Profile User { get; set; } = null!;
}
