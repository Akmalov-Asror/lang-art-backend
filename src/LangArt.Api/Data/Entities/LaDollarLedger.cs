namespace LangArt.Api.Data.Entities;

/// <summary>
/// Append-only ledger of every LA Dollar earn/spend/transfer. Mirrors the
/// XP ledger: positive amounts = earn, negative = spend or transfer-out.
/// The partial unique index on (user_id, reason, source_id) WHERE source_id
/// IS NOT NULL guards against duplicate awards.
/// </summary>
public class LaDollarLedger
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Profile User { get; set; } = null!;
}
