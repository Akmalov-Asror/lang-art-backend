using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.LaDollar.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace LangArt.Api.Features.LaDollar;

public class LaDollarService : ILaDollarService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LaDollarService> _logger;

    public LaDollarService(AppDbContext db, ILogger<LaDollarService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> AwardAsync(Guid userId, string reason, int amount, Guid? sourceId, string? description, CancellationToken ct)
    {
        if (amount == 0) return false;

        try
        {
            _db.LaDollarLedger.Add(new LaDollarLedger
            {
                UserId = userId,
                Amount = amount,
                Reason = reason,
                SourceId = sourceId,
                Description = description,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            foreach (var entry in _db.ChangeTracker.Entries<LaDollarLedger>().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }
            return false;
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_la_dollar_balances (user_id, total_balance, updated_at)
            VALUES ({userId}, {amount}, now())
            ON CONFLICT (user_id) DO UPDATE
                SET total_balance = user_la_dollar_balances.total_balance + EXCLUDED.total_balance,
                    updated_at = now()
        """, ct);

        return true;
    }

    public async Task<LaDollarBalanceDto> GetBalanceAsync(Guid userId, CancellationToken ct)
    {
        var row = await _db.UserLaDollarBalances.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);
        var recent = await _db.LaDollarLedger
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);
        return new LaDollarBalanceDto
        {
            UserId = userId,
            Balance = row?.TotalBalance ?? 0,
            UpdatedAt = row?.UpdatedAt ?? DateTime.UtcNow,
            RecentLedger = recent.Select(ToLedgerDto).ToList(),
        };
    }

    public async Task<IReadOnlyList<LaDollarLedgerEntryDto>> GetLedgerAsync(Guid userId, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200);
        var rows = await _db.LaDollarLedger
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
        return rows.Select(ToLedgerDto).ToList();
    }

    public async Task<IReadOnlyList<LaDollarStoreItemDto>> ListStoreAsync(bool includeInactive, CancellationToken ct)
    {
        var q = _db.LaDollarStoreItems.AsNoTracking();
        if (!includeInactive) q = q.Where(i => i.IsActive);
        var rows = await q.OrderBy(i => i.CostLaDollars).ToListAsync(ct);
        return rows.Select(ToItemDto).ToList();
    }

    public async Task<LaDollarStoreItemDto> CreateStoreItemAsync(UpsertStoreItemRequest req, CancellationToken ct)
    {
        var item = new LaDollarStoreItem
        {
            Type = req.Type,
            Title = req.Title,
            Description = req.Description,
            ImageUrl = req.ImageUrl,
            CostLaDollars = req.CostLaDollars,
            Stock = req.Stock,
            IsActive = req.IsActive,
        };
        _db.LaDollarStoreItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return ToItemDto(item);
    }

    public async Task<LaDollarStoreItemDto> UpdateStoreItemAsync(Guid id, UpsertStoreItemRequest req, CancellationToken ct)
    {
        var item = await _db.LaDollarStoreItems.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Item not found");
        item.Type = req.Type;
        item.Title = req.Title;
        item.Description = req.Description;
        item.ImageUrl = req.ImageUrl;
        item.CostLaDollars = req.CostLaDollars;
        item.Stock = req.Stock;
        item.IsActive = req.IsActive;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToItemDto(item);
    }

    public async Task DeleteStoreItemAsync(Guid id, CancellationToken ct)
    {
        var deleted = await _db.LaDollarStoreItems.Where(i => i.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new NotFoundException("Item not found");
    }

    public async Task<LaDollarPurchaseDto> PurchaseAsync(Guid userId, PurchaseItemRequest req, CancellationToken ct)
    {
        var item = await _db.LaDollarStoreItems.FirstOrDefaultAsync(i => i.Id == req.ItemId, ct)
            ?? throw new NotFoundException("Item not found");
        if (!item.IsActive) throw new BadRequestException("Item is not available");
        if (item.Stock is { } stock && stock <= 0)
            throw new BadRequestException("Item is out of stock");

        var balance = await _db.UserLaDollarBalances.AsNoTracking()
            .Where(b => b.UserId == userId).Select(b => (int?)b.TotalBalance).FirstOrDefaultAsync(ct) ?? 0;
        if (balance < item.CostLaDollars)
            throw new BadRequestException("Insufficient LA Dollars");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Deduct via ledger (negative). Idempotent on Id later when we create purchase row.
        var purchase = new LaDollarPurchase
        {
            UserId = userId,
            ItemId = item.Id,
            Cost = item.CostLaDollars,
            Status = "pending",
        };
        _db.LaDollarPurchases.Add(purchase);
        await _db.SaveChangesAsync(ct);

        await AwardAsync(userId, $"purchase_{item.Type}", -item.CostLaDollars, purchase.Id, item.Title, ct);

        if (item.Stock.HasValue)
        {
            item.Stock = item.Stock.Value - 1;
            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return await BuildPurchaseDto(purchase, ct);
    }

    public async Task<IReadOnlyList<LaDollarPurchaseDto>> ListMyPurchasesAsync(Guid userId, CancellationToken ct)
    {
        var rows = await _db.LaDollarPurchases
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Include(p => p.Item)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
        var names = await _db.Profiles.AsNoTracking()
            .Where(pr => pr.Id == userId)
            .Select(pr => pr.FullName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
        return rows.Select(p => ToPurchaseDto(p, names)).ToList();
    }

    public async Task<IReadOnlyList<LaDollarPurchaseDto>> ListAllPurchasesAsync(string? statusFilter, CancellationToken ct)
    {
        var q = _db.LaDollarPurchases.AsNoTracking().Include(p => p.Item).AsQueryable();
        if (!string.IsNullOrWhiteSpace(statusFilter)) q = q.Where(p => p.Status == statusFilter);
        var rows = await q.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var nameMap = await _db.Profiles.AsNoTracking()
            .Where(p => userIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FullName, ct);
        return rows.Select(p => ToPurchaseDto(p, nameMap.GetValueOrDefault(p.UserId) ?? "")).ToList();
    }

    public async Task<LaDollarPurchaseDto> FulfillPurchaseAsync(Guid purchaseId, CancellationToken ct)
    {
        var p = await _db.LaDollarPurchases.Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == purchaseId, ct)
            ?? throw new NotFoundException("Purchase not found");
        if (p.Status != "pending") throw new BadRequestException("Only pending purchases can be fulfilled");
        p.Status = "fulfilled";
        p.FulfilledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await BuildPurchaseDto(p, ct);
    }

    public async Task<LaDollarPurchaseDto> CancelPurchaseAsync(Guid purchaseId, CancellationToken ct)
    {
        var p = await _db.LaDollarPurchases.Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == purchaseId, ct)
            ?? throw new NotFoundException("Purchase not found");
        if (p.Status != "pending") throw new BadRequestException("Only pending purchases can be cancelled");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        p.Status = "cancelled";
        await _db.SaveChangesAsync(ct);
        // Refund the cost.
        await AwardAsync(p.UserId, "purchase_refund", p.Cost, p.Id, p.Item.Title, ct);
        if (p.Item.Stock.HasValue)
        {
            p.Item.Stock = p.Item.Stock.Value + 1;
            await _db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return await BuildPurchaseDto(p, ct);
    }

    public async Task<(LaDollarBalanceDto from, LaDollarBalanceDto to)> TransferAsync(Guid fromUserId, TransferRequest req, CancellationToken ct)
    {
        if (req.ToUserId == fromUserId) throw new BadRequestException("Cannot transfer to yourself");
        if (req.Amount <= 0) throw new BadRequestException("Amount must be positive");

        var toExists = await _db.Profiles.AnyAsync(p => p.Id == req.ToUserId, ct);
        if (!toExists) throw new NotFoundException("Recipient not found");

        var balance = await _db.UserLaDollarBalances.AsNoTracking()
            .Where(b => b.UserId == fromUserId).Select(b => (int?)b.TotalBalance).FirstOrDefaultAsync(ct) ?? 0;
        if (balance < req.Amount) throw new BadRequestException("Insufficient balance");

        // One synthetic source id binds both ledger rows so retries are idempotent.
        var transferId = Guid.NewGuid();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await AwardAsync(fromUserId, "transfer_sent", -req.Amount, transferId, req.Message, ct);
        await AwardAsync(req.ToUserId, "transfer_received", req.Amount, transferId, req.Message, ct);
        await tx.CommitAsync(ct);

        var from = await GetBalanceAsync(fromUserId, ct);
        var to = await GetBalanceAsync(req.ToUserId, ct);
        return (from, to);
    }

    public async Task<LaDollarBalanceDto> AdminGrantAsync(AdminGrantRequest req, CancellationToken ct)
    {
        await AwardAsync(req.UserId, req.Reason, req.Amount, Guid.NewGuid(), req.Description, ct);
        return await GetBalanceAsync(req.UserId, ct);
    }

    public async Task<LaDollarStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var inCirc = await _db.UserLaDollarBalances.SumAsync(b => (int?)b.TotalBalance, ct) ?? 0;
        var earned = await _db.LaDollarLedger.Where(l => l.Amount > 0).SumAsync(l => (int?)l.Amount, ct) ?? 0;
        var spent = await _db.LaDollarLedger.Where(l => l.Amount < 0).SumAsync(l => (int?)l.Amount, ct) ?? 0;

        var top = await (
            from b in _db.UserLaDollarBalances.AsNoTracking()
            join p in _db.Profiles.AsNoTracking() on b.UserId equals p.Id
            orderby b.TotalBalance descending
            select new LaDollarLeaderboardRow
            {
                UserId = b.UserId,
                FullName = p.FullName,
                Balance = b.TotalBalance,
            }).Take(10).ToListAsync(ct);

        return new LaDollarStatsDto
        {
            TotalInCirculation = inCirc,
            TotalEarnedAllTime = earned,
            TotalSpentAllTime = -spent,
            TopEarners = top,
        };
    }

    private async Task<LaDollarPurchaseDto> BuildPurchaseDto(LaDollarPurchase p, CancellationToken ct)
    {
        var userName = await _db.Profiles.AsNoTracking()
            .Where(pr => pr.Id == p.UserId).Select(pr => pr.FullName).FirstOrDefaultAsync(ct) ?? string.Empty;
        return ToPurchaseDto(p, userName);
    }

    private static LaDollarPurchaseDto ToPurchaseDto(LaDollarPurchase p, string userName) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        UserName = userName,
        Item = ToItemDto(p.Item),
        Cost = p.Cost,
        Status = p.Status,
        CreatedAt = p.CreatedAt,
        FulfilledAt = p.FulfilledAt,
    };

    private static LaDollarLedgerEntryDto ToLedgerDto(LaDollarLedger l) => new()
    {
        Id = l.Id,
        Amount = l.Amount,
        Reason = l.Reason,
        Description = l.Description,
        SourceId = l.SourceId,
        CreatedAtUtc = l.CreatedAtUtc,
    };

    private static LaDollarStoreItemDto ToItemDto(LaDollarStoreItem i) => new()
    {
        Id = i.Id,
        Type = i.Type,
        Title = i.Title,
        Description = i.Description,
        ImageUrl = i.ImageUrl,
        CostLaDollars = i.CostLaDollars,
        Stock = i.Stock,
        IsActive = i.IsActive,
        CreatedAt = i.CreatedAt,
    };
}
