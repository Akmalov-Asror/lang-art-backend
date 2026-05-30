namespace LangArt.Api.Data.Entities;

/// <summary>
/// Catalog item students can purchase with LA Dollars. <c>Stock</c> null means
/// unlimited (e.g. discount codes regenerated on demand); a positive integer
/// decrements per purchase.
/// </summary>
public class LaDollarStoreItem
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int CostLaDollars { get; set; }
    public int? Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
