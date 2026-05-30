using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Features.Finance.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Finance;

public class FinanceService : IFinanceService
{
    private static readonly string[] Kinds = { "income", "expense" };

    private readonly AppDbContext _db;

    public FinanceService(AppDbContext db) { _db = db; }

    // ===== Categories =====

    public async Task<IReadOnlyList<FinanceCategoryDto>> ListCategoriesAsync(string? kind, bool includeInactive, CancellationToken ct)
    {
        var q = _db.FinanceCategories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(kind))
        {
            EnsureValidKind(kind);
            q = q.Where(c => c.Kind == kind);
        }
        if (!includeInactive) q = q.Where(c => c.IsActive);

        var rows = await q.OrderBy(c => c.Kind).ThenBy(c => c.Name).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FinanceCategoryDto> CreateCategoryAsync(CreateFinanceCategoryRequest req, CancellationToken ct)
    {
        EnsureValidKind(req.Kind);
        var cat = new FinanceCategory
        {
            Name = req.Name.Trim(),
            Kind = req.Kind,
            Color = req.Color,
            IsActive = true,
        };
        _db.FinanceCategories.Add(cat);
        await _db.SaveChangesAsync(ct);
        return ToDto(cat);
    }

    public async Task<FinanceCategoryDto> UpdateCategoryAsync(Guid id, UpdateFinanceCategoryRequest req, CancellationToken ct)
    {
        var cat = await _db.FinanceCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category not found");
        cat.Name = req.Name.Trim();
        cat.Color = req.Color;
        cat.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return ToDto(cat);
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct)
    {
        var cat = await _db.FinanceCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Category not found");
        // Hard delete — FK on transactions is ON DELETE SET NULL so they stay
        // as "Uncategorized" rather than disappearing.
        _db.FinanceCategories.Remove(cat);
        await _db.SaveChangesAsync(ct);
    }

    // ===== Transactions =====

    public async Task<IReadOnlyList<FinanceTransactionDto>> ListTransactionsAsync(
        DateOnly? from, DateOnly? to, string? kind, Guid? categoryId, CancellationToken ct)
    {
        var q = _db.FinanceTransactions.AsNoTracking().AsQueryable();
        if (from is { } f) q = q.Where(t => t.OccurredOn >= f);
        if (to   is { } tt) q = q.Where(t => t.OccurredOn <= tt);
        if (!string.IsNullOrEmpty(kind)) { EnsureValidKind(kind); q = q.Where(t => t.Kind == kind); }
        if (categoryId is { } cid) q = q.Where(t => t.CategoryId == cid);

        var rows = await q
            .OrderByDescending(t => t.OccurredOn)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                Tx = t,
                CategoryName = t.Category != null ? t.Category.Name : null,
                CategoryColor = t.Category != null ? t.Category.Color : null,
                CreatedByName = t.CreatedByUser != null ? t.CreatedByUser.FullName : null,
            })
            .ToListAsync(ct);

        return rows.Select(r => new FinanceTransactionDto
        {
            Id = r.Tx.Id,
            OccurredOn = r.Tx.OccurredOn,
            Amount = r.Tx.Amount,
            Kind = r.Tx.Kind,
            CategoryId = r.Tx.CategoryId,
            CategoryName = r.CategoryName,
            CategoryColor = r.CategoryColor,
            Description = r.Tx.Description,
            CreatedByName = r.CreatedByName,
            CreatedAt = r.Tx.CreatedAt,
        }).ToList();
    }

    public async Task<FinanceTransactionDto> CreateTransactionAsync(Guid currentUserId, CreateFinanceTransactionRequest req, CancellationToken ct)
    {
        EnsureValidKind(req.Kind);
        if (req.Amount < 0) throw new BadRequestException("Amount must be non-negative");

        if (req.CategoryId is { } cid)
        {
            var cat = await _db.FinanceCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct)
                ?? throw new NotFoundException("Category not found");
            if (cat.Kind != req.Kind)
                throw new BadRequestException($"Category kind ({cat.Kind}) does not match transaction kind ({req.Kind})");
        }

        var tx = new FinanceTransaction
        {
            OccurredOn = req.OccurredOn,
            Amount = req.Amount,
            Kind = req.Kind,
            CategoryId = req.CategoryId,
            Description = req.Description,
            CreatedBy = currentUserId,
        };
        _db.FinanceTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);
        return await ReloadDto(tx.Id, ct);
    }

    public async Task<FinanceTransactionDto> UpdateTransactionAsync(Guid id, UpdateFinanceTransactionRequest req, CancellationToken ct)
    {
        EnsureValidKind(req.Kind);
        var tx = await _db.FinanceTransactions.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Transaction not found");

        if (req.CategoryId is { } cid)
        {
            var cat = await _db.FinanceCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct)
                ?? throw new NotFoundException("Category not found");
            if (cat.Kind != req.Kind)
                throw new BadRequestException($"Category kind ({cat.Kind}) does not match transaction kind ({req.Kind})");
        }

        tx.OccurredOn = req.OccurredOn;
        tx.Amount = req.Amount;
        tx.Kind = req.Kind;
        tx.CategoryId = req.CategoryId;
        tx.Description = req.Description;
        await _db.SaveChangesAsync(ct);
        return await ReloadDto(tx.Id, ct);
    }

    public async Task DeleteTransactionAsync(Guid id, CancellationToken ct)
    {
        var deleted = await _db.FinanceTransactions
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new NotFoundException("Transaction not found");
    }

    // ===== Summary =====

    public async Task<FinanceSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rangeFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        var rangeTo   = to   ?? today;

        var q = _db.FinanceTransactions.AsNoTracking()
            .Where(t => t.OccurredOn >= rangeFrom && t.OccurredOn <= rangeTo);

        var totals = await q
            .GroupBy(t => t.Kind)
            .Select(g => new { Kind = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        var income = totals.FirstOrDefault(t => t.Kind == "income")?.Total ?? 0m;
        var expense = totals.FirstOrDefault(t => t.Kind == "expense")?.Total ?? 0m;

        var byCategory = await q
            .GroupBy(t => new { t.CategoryId, t.Kind })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.Kind,
                Total = g.Sum(x => x.Amount),
                Count = g.Count(),
                Name = g.Key.CategoryId == null
                    ? "Uncategorized"
                    : _db.FinanceCategories.Where(c => c.Id == g.Key.CategoryId).Select(c => c.Name).FirstOrDefault() ?? "Uncategorized",
                Color = g.Key.CategoryId == null
                    ? null
                    : _db.FinanceCategories.Where(c => c.Id == g.Key.CategoryId).Select(c => c.Color).FirstOrDefault(),
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        return new FinanceSummaryDto
        {
            From = rangeFrom,
            To = rangeTo,
            TotalIncome = income,
            TotalExpense = expense,
            Balance = income - expense,
            ByCategory = byCategory.Select(x => new FinanceByCategoryDto
            {
                CategoryId = x.CategoryId,
                CategoryName = x.Name,
                CategoryColor = x.Color,
                Kind = x.Kind,
                Total = x.Total,
                Count = x.Count,
            }).ToList(),
        };
    }

    // ===== helpers =====

    private static void EnsureValidKind(string kind)
    {
        if (!Kinds.Contains(kind))
            throw new BadRequestException($"Invalid kind '{kind}'. Must be one of: {string.Join(", ", Kinds)}");
    }

    private async Task<FinanceTransactionDto> ReloadDto(Guid id, CancellationToken ct)
    {
        var row = await _db.FinanceTransactions.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                Tx = t,
                CategoryName = t.Category != null ? t.Category.Name : null,
                CategoryColor = t.Category != null ? t.Category.Color : null,
                CreatedByName = t.CreatedByUser != null ? t.CreatedByUser.FullName : null,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Transaction not found after save");

        return new FinanceTransactionDto
        {
            Id = row.Tx.Id,
            OccurredOn = row.Tx.OccurredOn,
            Amount = row.Tx.Amount,
            Kind = row.Tx.Kind,
            CategoryId = row.Tx.CategoryId,
            CategoryName = row.CategoryName,
            CategoryColor = row.CategoryColor,
            Description = row.Tx.Description,
            CreatedByName = row.CreatedByName,
            CreatedAt = row.Tx.CreatedAt,
        };
    }

    private static FinanceCategoryDto ToDto(FinanceCategory c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Kind = c.Kind,
        Color = c.Color,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
    };
}
