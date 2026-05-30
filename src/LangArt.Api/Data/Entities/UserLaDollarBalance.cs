namespace LangArt.Api.Data.Entities;

/// <summary>
/// One row per user. Denormalised running total of LA Dollars; always equal
/// to <c>SUM(la_dollar_ledger.amount) WHERE user_id = …</c>. Mirrors the
/// <see cref="UserXp"/> pattern.
/// </summary>
public class UserLaDollarBalance
{
    public Guid UserId { get; set; }
    public int TotalBalance { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Profile User { get; set; } = null!;
}
