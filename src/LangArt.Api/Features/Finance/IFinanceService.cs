using LangArt.Api.Features.Finance.Dto;

namespace LangArt.Api.Features.Finance;

public interface IFinanceService
{
    Task<IReadOnlyList<FinanceCategoryDto>> ListCategoriesAsync(string? kind, bool includeInactive, CancellationToken ct);
    Task<FinanceCategoryDto> CreateCategoryAsync(CreateFinanceCategoryRequest req, CancellationToken ct);
    Task<FinanceCategoryDto> UpdateCategoryAsync(Guid id, UpdateFinanceCategoryRequest req, CancellationToken ct);
    Task DeleteCategoryAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<FinanceTransactionDto>> ListTransactionsAsync(
        DateOnly? from, DateOnly? to, string? kind, Guid? categoryId,
        CancellationToken ct);

    Task<FinanceTransactionDto> CreateTransactionAsync(Guid currentUserId, CreateFinanceTransactionRequest req, CancellationToken ct);
    Task<FinanceTransactionDto> UpdateTransactionAsync(Guid id, UpdateFinanceTransactionRequest req, CancellationToken ct);
    Task DeleteTransactionAsync(Guid id, CancellationToken ct);

    Task<FinanceSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct);
}
