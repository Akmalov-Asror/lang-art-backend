namespace LangArt.Api.Data.Entities;

/// <summary>
/// A user-defined category for finance transactions. Each category belongs
/// to exactly one kind: "income" or "expense". Soft-deleted via IsActive so
/// historical transactions keep their reference.
/// </summary>
public class FinanceCategory
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "income"; // "income" | "expense"
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
