using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Finance.Dto;

public class FinanceCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "income";
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFinanceCategoryRequest
{
    [Required, MinLength(1), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Kind { get; set; } = "income";

    [MaxLength(20)]
    public string? Color { get; set; }
}

public class UpdateFinanceCategoryRequest
{
    [Required, MinLength(1), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Color { get; set; }

    public bool IsActive { get; set; } = true;
}

public class FinanceTransactionDto
{
    public Guid Id { get; set; }
    public DateOnly OccurredOn { get; set; }
    public decimal Amount { get; set; }
    public string Kind { get; set; } = "income";
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public string? Description { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFinanceTransactionRequest
{
    [Required]
    public DateOnly OccurredOn { get; set; }

    [Required, Range(0, 999_999_999_999)]
    public decimal Amount { get; set; }

    [Required]
    public string Kind { get; set; } = "income";

    public Guid? CategoryId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class UpdateFinanceTransactionRequest : CreateFinanceTransactionRequest { }

public class FinanceSummaryDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Balance { get; set; }
    public List<FinanceByCategoryDto> ByCategory { get; set; } = new();
}

public class FinanceByCategoryDto
{
    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = "Uncategorized";
    public string? CategoryColor { get; set; }
    public string Kind { get; set; } = "income";
    public decimal Total { get; set; }
    public int Count { get; set; }
}
