using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Gamification.Dto;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LangArt.Api.Features.Gamification;

public class GamificationService : IGamificationService
{
    private const int StreakBonusPerDay = 5;
    private const int StreakBonusCap = 50;

    private readonly AppDbContext _db;
    private readonly ILogger<GamificationService> _logger;
    private readonly IServiceProvider _services;

    public GamificationService(AppDbContext db, ILogger<GamificationService> logger, IServiceProvider services)
    {
        _db = db;
        _logger = logger;
        _services = services;
    }

    public async Task<bool> AwardXpAsync(Guid userId, XpReason reason, int amount, Guid? sourceId, CancellationToken ct)
    {
        if (amount == 0) return false;

        // Insert the ledger row first. If a duplicate violates one of the partial
        // unique indexes, Npgsql will raise PostgresException with SqlState 23505 —
        // we catch that and return false (silent no-op = "already awarded").
        try
        {
            _db.XpLedger.Add(new XpLedger
            {
                UserId = userId,
                Amount = amount,
                Reason = reason,
                SourceId = sourceId,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            // Detach the failed insert so the context can keep being used.
            foreach (var entry in _db.ChangeTracker.Entries<XpLedger>().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }
            return false;
        }

        // Upsert the denormalised total. EF Core 7+ ExecuteUpdate would be nicer
        // but we need INSERT-or-update semantics — raw SQL keeps it atomic.
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_xp (user_id, total_xp, updated_at)
            VALUES ({userId}, {amount}, now())
            ON CONFLICT (user_id) DO UPDATE
                SET total_xp = user_xp.total_xp + EXCLUDED.total_xp,
                    updated_at = now()
        """, ct);

        return true;
    }

    public async Task<StreakResult> RecordActivityAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        var streak = await _db.UserStreaks.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (streak is null)
        {
            streak = new UserStreak
            {
                UserId = userId,
                CurrentStreak = 1,
                LongestStreak = 1,
                LastActivityDateUtc = today,
            };
            _db.UserStreaks.Add(streak);
            await _db.SaveChangesAsync(ct);
            await AwardStreakBonusAsync(userId, streak.CurrentStreak, today, ct);
            return new StreakResult
            {
                CurrentStreak = 1,
                LongestStreak = 1,
                IsNewRecord = true,
                AwardedBonusXp = ClampStreakBonus(1),
            };
        }

        if (streak.LastActivityDateUtc == today)
        {
            // Already counted today — no-op.
            return new StreakResult
            {
                CurrentStreak = streak.CurrentStreak,
                LongestStreak = streak.LongestStreak,
                IsNewRecord = false,
                AwardedBonusXp = 0,
            };
        }

        if (streak.LastActivityDateUtc == yesterday)
        {
            streak.CurrentStreak += 1;
        }
        else
        {
            streak.CurrentStreak = 1;
        }
        var wasNewRecord = streak.CurrentStreak > streak.LongestStreak;
        if (wasNewRecord) streak.LongestStreak = streak.CurrentStreak;
        streak.LastActivityDateUtc = today;
        await _db.SaveChangesAsync(ct);

        var bonusXp = ClampStreakBonus(streak.CurrentStreak);
        if (bonusXp > 0)
        {
            await AwardStreakBonusAsync(userId, streak.CurrentStreak, today, ct);
        }

        return new StreakResult
        {
            CurrentStreak = streak.CurrentStreak,
            LongestStreak = streak.LongestStreak,
            IsNewRecord = wasNewRecord,
            AwardedBonusXp = bonusXp,
        };
    }

    public async Task<IReadOnlyList<BadgeDto>> EvaluateBadgesAsync(Guid userId, CancellationToken ct)
    {
        var allBadges = await _db.Badges.AsNoTracking().ToListAsync(ct);
        var alreadyEarned = await _db.UserBadges
            .Where(ub => ub.UserId == userId)
            .Select(ub => ub.BadgeId)
            .ToListAsync(ct);
        var earnedSet = alreadyEarned.ToHashSet();

        var newlyEarned = new List<BadgeDto>();
        foreach (var badge in allBadges)
        {
            if (earnedSet.Contains(badge.Id)) continue;
            try
            {
                if (!await BadgeCriteriaEvaluator.IsMetAsync(badge.Code, userId, _db, ct)) continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Badge criteria evaluation failed for {Code} / user {UserId}", badge.Code, userId);
                continue;
            }

            _db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = badge.Id });
            await _db.SaveChangesAsync(ct);

            if (badge.XpReward > 0)
            {
                await AwardXpAsync(userId, XpReason.BadgeReward, badge.XpReward, badge.Id, ct);
            }

            newlyEarned.Add(new BadgeDto
            {
                Id = badge.Id,
                Code = badge.Code,
                Name = badge.Name,
                Description = badge.Description,
                IconUrl = badge.IconUrl,
                XpReward = badge.XpReward,
                Earned = true,
                EarnedAtUtc = DateTime.UtcNow,
            });
        }

        // Best-effort dual delivery:
        //   1. Web Push (Sprint 1)            — reaches offline users via the OS notification daemon
        //   2. SignalR BadgeEarned event       — reaches currently-connected tabs instantly
        // Both wrapped so a failure in either path can't fail the originating transaction
        // (the badge itself is already persisted by the time we get here).
        if (newlyEarned.Count > 0)
        {
            var push = _services.GetService<Notifications.Push.IPushNotificationService>();
            var dispatcher = _services.GetService<Realtime.INotificationDispatcher>();

            foreach (var b in newlyEarned)
            {
                if (push is not null)
                {
                    try
                    {
                        await push.SendToUserAsync(userId, new Notifications.Push.PushPayload
                        {
                            Title = $"You earned “{b.Name}”!",
                            Body = b.Description,
                            Tag = $"badge:{b.Code}",
                            Url = "/dashboard",
                        }, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Push notification for badge {Code} failed; user still earned the badge", b.Code);
                    }
                }

                if (dispatcher is not null)
                {
                    await dispatcher.SendBadgeEarnedAsync(userId, new Realtime.Dto.BadgeEarnedDto
                    {
                        Badge = new Realtime.Dto.BadgePayload
                        {
                            Id = b.Id,
                            Code = b.Code,
                            Name = b.Name,
                            Description = b.Description,
                            IconUrl = b.IconUrl,
                            XpReward = b.XpReward,
                        },
                        EarnedAtUtc = b.EarnedAtUtc ?? DateTime.UtcNow,
                    }, ct);
                }
            }
        }

        return newlyEarned;
    }

    public async Task<GamificationProfileDto> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var xp = await _db.UserXp.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        var streak = await _db.UserStreaks.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);

        var totalXp = xp?.TotalXp ?? 0;
        var progress = LevelCalculator.Progress(totalXp);

        var earnedBadges = await _db.UserBadges
            .AsNoTracking()
            .Where(ub => ub.UserId == userId)
            .Include(ub => ub.Badge)
            .OrderByDescending(ub => ub.EarnedAtUtc)
            .ToListAsync(ct);

        var recent = await _db.XpLedger
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(5)
            .ToListAsync(ct);

        return new GamificationProfileDto
        {
            UserId = userId,
            TotalXp = totalXp,
            Level = progress.Level,
            XpInLevel = progress.XpInLevel,
            XpForNextLevel = progress.XpForNextLevel,
            CurrentStreak = streak?.CurrentStreak ?? 0,
            LongestStreak = streak?.LongestStreak ?? 0,
            LastActivityDateUtc = streak?.LastActivityDateUtc,
            EarnedBadges = earnedBadges.Select(ub => new BadgeDto
            {
                Id = ub.Badge.Id,
                Code = ub.Badge.Code,
                Name = ub.Badge.Name,
                Description = ub.Badge.Description,
                IconUrl = ub.Badge.IconUrl,
                XpReward = ub.Badge.XpReward,
                Earned = true,
                EarnedAtUtc = ub.EarnedAtUtc,
            }).ToList(),
            RecentLedger = recent.Select(ToLedgerDto).ToList(),
        };
    }

    public async Task<LeaderboardDto> GetGroupLeaderboardAsync(Guid groupId, Guid currentUserId, CancellationToken ct)
    {
        var groupExists = await _db.Groups.AnyAsync(g => g.Id == groupId, ct);
        if (!groupExists) throw new NotFoundException("Group not found");

        var memberIds = await _db.GroupStudents
            .Where(gs => gs.GroupId == groupId)
            .Select(gs => gs.StudentId)
            .ToListAsync(ct);

        // Pull XP + profile for each member in one round-trip.
        var rows = await (
            from p in _db.Profiles.AsNoTracking()
            where memberIds.Contains(p.Id)
            join x in _db.UserXp.AsNoTracking() on p.Id equals x.UserId into xl
            from x in xl.DefaultIfEmpty()
            select new
            {
                p.Id,
                p.FullName,
                p.AvatarUrl,
                TotalXp = x == null ? 0 : x.TotalXp,
            })
            .ToListAsync(ct);

        var ranked = rows
            .OrderByDescending(r => r.TotalXp)
            .ThenBy(r => r.FullName)
            .Select((r, i) => new LeaderboardRowDto
            {
                Rank = i + 1,
                UserId = r.Id,
                FullName = r.FullName,
                AvatarUrl = r.AvatarUrl,
                TotalXp = r.TotalXp,
                Level = LevelCalculator.GetLevelFromXp(r.TotalXp),
                IsCurrentUser = r.Id == currentUserId,
            })
            .ToList();

        return new LeaderboardDto
        {
            GroupId = groupId,
            Rows = ranked,
            CurrentUserRank = ranked.FirstOrDefault(r => r.IsCurrentUser)?.Rank,
        };
    }

    public async Task<IReadOnlyList<BadgeDto>> GetBadgeCatalogAsync(Guid currentUserId, CancellationToken ct)
    {
        var all = await _db.Badges.AsNoTracking().OrderBy(b => b.CreatedAt).ToListAsync(ct);
        var earned = await _db.UserBadges
            .AsNoTracking()
            .Where(ub => ub.UserId == currentUserId)
            .ToDictionaryAsync(ub => ub.BadgeId, ub => ub.EarnedAtUtc, ct);

        return all.Select(b => new BadgeDto
        {
            Id = b.Id,
            Code = b.Code,
            Name = b.Name,
            Description = b.Description,
            IconUrl = b.IconUrl,
            XpReward = b.XpReward,
            Earned = earned.ContainsKey(b.Id),
            EarnedAtUtc = earned.TryGetValue(b.Id, out var d) ? d : null,
        }).ToList();
    }

    public async Task<IReadOnlyList<LedgerEntryDto>> GetRecentLedgerAsync(Guid userId, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        var rows = await _db.XpLedger
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
        return rows.Select(ToLedgerDto).ToList();
    }

    private static LedgerEntryDto ToLedgerDto(XpLedger l) => new()
    {
        Id = l.Id,
        Amount = l.Amount,
        Reason = l.Reason,
        SourceId = l.SourceId,
        CreatedAtUtc = l.CreatedAtUtc,
    };

    private static int ClampStreakBonus(int currentStreak) =>
        Math.Min(currentStreak * StreakBonusPerDay, StreakBonusCap);

    private async Task AwardStreakBonusAsync(Guid userId, int currentStreak, DateOnly today, CancellationToken ct)
    {
        var bonus = ClampStreakBonus(currentStreak);
        if (bonus <= 0) return;
        // Use the date as a synthetic source_id-ish key by hashing into a Guid? No —
        // simpler: streak bonuses are not blocked by unique index (source_id is null
        // and reason != daily_login). We rely on the once-per-day no-op in
        // RecordActivityAsync to keep this idempotent.
        await AwardXpAsync(userId, XpReason.StreakBonus, bonus, sourceId: null, ct);
    }
}
