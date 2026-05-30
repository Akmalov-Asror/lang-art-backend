namespace LangArt.Api.Data.Entities;

/// <summary>
/// A single bookkeeping entry — either income (prixod) or expense (chiqim).
/// Currency is implicit (UZS) for now; surface explicitly when multi-currency
/// support is needed.
/// </summary>
public class FinanceTransaction
{
    public Guid Id { get; set; }
    public DateOnly OccurredOn { get; set; }
    public decimal Amount { get; set; }
    public string Kind { get; set; } = "income"; // "income" | "expense"
    public Guid? CategoryId { get; set; }
    public string? Description { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public FinanceCategory? Category { get; set; }
    public Profile? CreatedByUser { get; set; }
}
