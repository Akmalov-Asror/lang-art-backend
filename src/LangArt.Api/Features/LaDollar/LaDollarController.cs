using LangArt.Api.Common.Auth;
using LangArt.Api.Features.LaDollar.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.LaDollar;

[ApiController]
[Route("api/la-dollar")]
[Authorize]
public class LaDollarController : ControllerBase
{
    private readonly ILaDollarService _svc;
    private readonly ICurrentUser _currentUser;

    public LaDollarController(ILaDollarService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    // ===== Student =====

    [HttpGet("me")]
    public Task<LaDollarBalanceDto> Me(CancellationToken ct) =>
        _svc.GetBalanceAsync(_currentUser.Id, ct);

    [HttpGet("me/ledger")]
    public Task<IReadOnlyList<LaDollarLedgerEntryDto>> MyLedger([FromQuery] int limit = 50, CancellationToken ct = default) =>
        _svc.GetLedgerAsync(_currentUser.Id, limit, ct);

    [HttpGet("store")]
    public Task<IReadOnlyList<LaDollarStoreItemDto>> Store(CancellationToken ct) =>
        _svc.ListStoreAsync(includeInactive: false, ct);

    [HttpPost("store/purchase")]
    public Task<LaDollarPurchaseDto> Purchase([FromBody] PurchaseItemRequest req, CancellationToken ct) =>
        _svc.PurchaseAsync(_currentUser.Id, req, ct);

    [HttpGet("me/purchases")]
    public Task<IReadOnlyList<LaDollarPurchaseDto>> MyPurchases(CancellationToken ct) =>
        _svc.ListMyPurchasesAsync(_currentUser.Id, ct);

    [HttpPost("transfer")]
    public async Task<object> Transfer([FromBody] TransferRequest req, CancellationToken ct)
    {
        var (from, to) = await _svc.TransferAsync(_currentUser.Id, req, ct);
        return new { from, to };
    }
}

[ApiController]
[Route("api/admin/la-dollar")]
[Authorize(Roles = "admin")]
public class AdminLaDollarController : ControllerBase
{
    private readonly ILaDollarService _svc;

    public AdminLaDollarController(ILaDollarService svc)
    {
        _svc = svc;
    }

    [HttpGet("store")]
    public Task<IReadOnlyList<LaDollarStoreItemDto>> Store(CancellationToken ct) =>
        _svc.ListStoreAsync(includeInactive: true, ct);

    [HttpPost("store")]
    public Task<LaDollarStoreItemDto> CreateItem([FromBody] UpsertStoreItemRequest req, CancellationToken ct) =>
        _svc.CreateStoreItemAsync(req, ct);

    [HttpPut("store/{id:guid}")]
    public Task<LaDollarStoreItemDto> UpdateItem(Guid id, [FromBody] UpsertStoreItemRequest req, CancellationToken ct) =>
        _svc.UpdateStoreItemAsync(id, req, ct);

    [HttpDelete("store/{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
    {
        await _svc.DeleteStoreItemAsync(id, ct);
        return NoContent();
    }

    [HttpGet("purchases")]
    public Task<IReadOnlyList<LaDollarPurchaseDto>> Purchases([FromQuery] string? status, CancellationToken ct) =>
        _svc.ListAllPurchasesAsync(status, ct);

    [HttpPost("purchases/{id:guid}/fulfill")]
    public Task<LaDollarPurchaseDto> Fulfill(Guid id, CancellationToken ct) =>
        _svc.FulfillPurchaseAsync(id, ct);

    [HttpPost("purchases/{id:guid}/cancel")]
    public Task<LaDollarPurchaseDto> Cancel(Guid id, CancellationToken ct) =>
        _svc.CancelPurchaseAsync(id, ct);

    [HttpPost("grant")]
    public Task<LaDollarBalanceDto> Grant([FromBody] AdminGrantRequest req, CancellationToken ct) =>
        _svc.AdminGrantAsync(req, ct);

    [HttpGet("stats")]
    public Task<LaDollarStatsDto> Stats(CancellationToken ct) =>
        _svc.GetStatsAsync(ct);
}
