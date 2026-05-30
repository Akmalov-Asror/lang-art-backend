using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Finance.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Finance;

/// <summary>
/// Admin-only bookkeeping endpoints: categories and transactions (income/expense)
/// plus a date-ranged summary used by the dashboard cards.
/// </summary>
[ApiController]
[Route("api/admin/finance")]
[Authorize(Roles = "admin")]
public class FinanceController : ControllerBase
{
    private readonly IFinanceService _svc;
    private readonly ICurrentUser _currentUser;

    public FinanceController(IFinanceService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    // ----- Categories -----

    [HttpGet("categories")]
    public Task<IReadOnlyList<FinanceCategoryDto>> ListCategories(
        [FromQuery] string? kind, [FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        _svc.ListCategoriesAsync(kind, includeInactive, ct);

    [HttpPost("categories")]
    public Task<FinanceCategoryDto> CreateCategory([FromBody] CreateFinanceCategoryRequest req, CancellationToken ct) =>
        _svc.CreateCategoryAsync(req, ct);

    [HttpPut("categories/{id:guid}")]
    public Task<FinanceCategoryDto> UpdateCategory(Guid id, [FromBody] UpdateFinanceCategoryRequest req, CancellationToken ct) =>
        _svc.UpdateCategoryAsync(id, req, ct);

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        await _svc.DeleteCategoryAsync(id, ct);
        return NoContent();
    }

    // ----- Transactions -----

    [HttpGet("transactions")]
    public Task<IReadOnlyList<FinanceTransactionDto>> ListTransactions(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? kind, [FromQuery] Guid? categoryId,
        CancellationToken ct = default) =>
        _svc.ListTransactionsAsync(from, to, kind, categoryId, ct);

    [HttpPost("transactions")]
    public Task<FinanceTransactionDto> CreateTransaction([FromBody] CreateFinanceTransactionRequest req, CancellationToken ct) =>
        _svc.CreateTransactionAsync(_currentUser.Id, req, ct);

    [HttpPut("transactions/{id:guid}")]
    public Task<FinanceTransactionDto> UpdateTransaction(Guid id, [FromBody] UpdateFinanceTransactionRequest req, CancellationToken ct) =>
        _svc.UpdateTransactionAsync(id, req, ct);

    [HttpDelete("transactions/{id:guid}")]
    public async Task<IActionResult> DeleteTransaction(Guid id, CancellationToken ct)
    {
        await _svc.DeleteTransactionAsync(id, ct);
        return NoContent();
    }

    // ----- Summary -----

    [HttpGet("summary")]
    public Task<FinanceSummaryDto> Summary(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct = default) =>
        _svc.GetSummaryAsync(from, to, ct);
}
