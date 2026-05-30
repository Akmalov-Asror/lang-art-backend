using LangArt.Api.Features.LaDollar.Dto;

namespace LangArt.Api.Features.LaDollar;

public interface ILaDollarService
{
    /// <summary>
    /// Inserts a ledger row and updates the denormalised balance. Mirrors
    /// GamificationService.AwardXpAsync — the unique partial index on
    /// (user_id, reason, source_id) WHERE source_id IS NOT NULL silently
    /// no-ops duplicate awards. Returns true if a row was actually written.
    /// Negative amounts are allowed (spend / admin adjustment).
    /// </summary>
    Task<bool> AwardAsync(Guid userId, string reason, int amount, Guid? sourceId, string? description, CancellationToken ct);

    Task<LaDollarBalanceDto> GetBalanceAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<LaDollarLedgerEntryDto>> GetLedgerAsync(Guid userId, int limit, CancellationToken ct);

    Task<IReadOnlyList<LaDollarStoreItemDto>> ListStoreAsync(bool includeInactive, CancellationToken ct);
    Task<LaDollarStoreItemDto> CreateStoreItemAsync(UpsertStoreItemRequest req, CancellationToken ct);
    Task<LaDollarStoreItemDto> UpdateStoreItemAsync(Guid id, UpsertStoreItemRequest req, CancellationToken ct);
    Task DeleteStoreItemAsync(Guid id, CancellationToken ct);

    Task<LaDollarPurchaseDto> PurchaseAsync(Guid userId, PurchaseItemRequest req, CancellationToken ct);
    Task<IReadOnlyList<LaDollarPurchaseDto>> ListMyPurchasesAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<LaDollarPurchaseDto>> ListAllPurchasesAsync(string? statusFilter, CancellationToken ct);
    Task<LaDollarPurchaseDto> FulfillPurchaseAsync(Guid purchaseId, CancellationToken ct);
    Task<LaDollarPurchaseDto> CancelPurchaseAsync(Guid purchaseId, CancellationToken ct);

    Task<(LaDollarBalanceDto from, LaDollarBalanceDto to)> TransferAsync(Guid fromUserId, TransferRequest req, CancellationToken ct);

    Task<LaDollarBalanceDto> AdminGrantAsync(AdminGrantRequest req, CancellationToken ct);
    Task<LaDollarStatsDto> GetStatsAsync(CancellationToken ct);
}
