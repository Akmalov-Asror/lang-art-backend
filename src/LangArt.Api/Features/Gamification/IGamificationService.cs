using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Gamification.Dto;

namespace LangArt.Api.Features.Gamification;

public interface IGamificationService
{
    /// <summary>
    /// Inserts a ledger row + bumps <c>user_xp.total_xp</c>. For <paramref name="sourceId"/>
    /// non-null, the unique partial index on <c>(user_id, reason, source_id)</c> guarantees
    /// at most one award per (user, reason, source); a duplicate is a silent no-op.
    /// For <see cref="XpReason.DailyLogin"/> with null source, the
    /// <c>uq_xp_ledger_daily_login</c> partial index caps to one per UTC day.
    /// </summary>
    /// <returns><c>true</c> if XP was actually awarded (i.e. not a duplicate).</returns>
    Task<bool> AwardXpAsync(Guid userId, XpReason reason, int amount, Guid? sourceId, CancellationToken ct);

    /// <summary>Increments / resets the user's daily streak. Idempotent within a single UTC day.</summary>
    Task<StreakResult> RecordActivityAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Re-evaluates all unmet badge criteria for this user, persists the new ones
    /// and returns them so the caller can surface a "you earned X!" toast.
    /// </summary>
    Task<IReadOnlyList<BadgeDto>> EvaluateBadgesAsync(Guid userId, CancellationToken ct);

    Task<GamificationProfileDto> GetProfileAsync(Guid userId, CancellationToken ct);

    Task<LeaderboardDto> GetGroupLeaderboardAsync(Guid groupId, Guid currentUserId, CancellationToken ct);

    Task<IReadOnlyList<BadgeDto>> GetBadgeCatalogAsync(Guid currentUserId, CancellationToken ct);

    Task<IReadOnlyList<LedgerEntryDto>> GetRecentLedgerAsync(Guid userId, int limit, CancellationToken ct);
}
