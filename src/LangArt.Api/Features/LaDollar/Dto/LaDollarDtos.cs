using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.LaDollar.Dto;

public class LaDollarBalanceDto
{
    public Guid UserId { get; set; }
    public int Balance { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<LaDollarLedgerEntryDto> RecentLedger { get; set; } = new();
}

public class LaDollarLedgerEntryDto
{
    public Guid Id { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? SourceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class LaDollarStoreItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int CostLaDollars { get; set; }
    public int? Stock { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LaDollarPurchaseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public LaDollarStoreItemDto Item { get; set; } = new();
    public int Cost { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
}

public class UpsertStoreItemRequest
{
    [Required, RegularExpression("^(discount|book|coffee|extra_lesson|other)$")]
    public string Type { get; set; } = "other";

    [Required, MinLength(1), MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    [Required, Range(1, 1_000_000)]
    public int CostLaDollars { get; set; }

    public int? Stock { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PurchaseItemRequest
{
    [Required]
    public Guid ItemId { get; set; }
}

public class TransferRequest
{
    [Required]
    public Guid ToUserId { get; set; }

    [Required, Range(1, 1_000_000)]
    public int Amount { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }
}

public class AdminGrantRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required, Range(-1_000_000, 1_000_000)]
    public int Amount { get; set; }

    [MaxLength(500)]
    public string Reason { get; set; } = "admin_adjustment";

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class LaDollarStatsDto
{
    public int TotalInCirculation { get; set; }
    public int TotalEarnedAllTime { get; set; }
    public int TotalSpentAllTime { get; set; }
    public List<LaDollarLeaderboardRow> TopEarners { get; set; } = new();
}

public class LaDollarLeaderboardRow
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Balance { get; set; }
}
