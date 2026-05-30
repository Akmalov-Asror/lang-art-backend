using LangArt.Api.Data.Enums;

namespace LangArt.Api.Data.Entities;

/// <summary>
/// Append-only audit log of every XP event. The denormalised <c>user_xp.total_xp</c>
/// counter must always equal <c>SUM(amount) GROUP BY user_id</c> for that user;
/// reconstructing from the ledger is the disaster-recovery path.
///
/// Idempotency:
/// <list type="bullet">
///   <item>For events with a meaningful source (lesson completion, quiz submission)
///         a unique index on <c>(user_id, reason, source_id)</c> WHERE
///         <c>source_id IS NOT NULL</c> blocks duplicate awards.</item>
///   <item>For <see cref="XpReason.DailyLogin"/> (where <c>source_id</c> is null)
///         a separate unique index on <c>(user_id, created_at_utc::date)</c>
///         WHERE <c>reason = 'daily_login'</c> caps it at one per UTC day.</item>
/// </list>
/// </summary>
public class XpLedger
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public XpReason Reason { get; set; }
    public Guid? SourceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Profile User { get; set; } = null!;
}
