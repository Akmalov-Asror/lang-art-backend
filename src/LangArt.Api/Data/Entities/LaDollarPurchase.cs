namespace LangArt.Api.Data.Entities;

/// <summary>
/// One row per student purchase. Status transitions:
///   pending → fulfilled (admin clicked "delivered")
///   pending → cancelled (refunds the cost back to the ledger)
/// </summary>
public class LaDollarPurchase
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public int Cost { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }

    public Profile User { get; set; } = null!;
    public LaDollarStoreItem Item { get; set; } = null!;
}
