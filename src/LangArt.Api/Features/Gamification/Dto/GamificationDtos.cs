using LangArt.Api.Data.Enums;

namespace LangArt.Api.Features.Gamification.Dto;

public class StreakResult
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public bool IsNewRecord { get; set; }
    public int AwardedBonusXp { get; set; }
}

public class BadgeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public int XpReward { get; set; }
    public bool Earned { get; set; }
    public DateTime? EarnedAtUtc { get; set; }
}

public class LedgerEntryDto
{
    public Guid Id { get; set; }
    public int Amount { get; set; }
    public XpReason Reason { get; set; }
    public Guid? SourceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class GamificationProfileDto
{
    public Guid UserId { get; set; }
    public int TotalXp { get; set; }
    public int Level { get; set; }
    public int XpInLevel { get; set; }
    public int XpForNextLevel { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateOnly? LastActivityDateUtc { get; set; }
    public List<BadgeDto> EarnedBadges { get; set; } = new();
    public List<LedgerEntryDto> RecentLedger { get; set; } = new();
}

public class LeaderboardRowDto
{
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int TotalXp { get; set; }
    public int Level { get; set; }
    public bool IsCurrentUser { get; set; }
}

public class LeaderboardDto
{
    public Guid GroupId { get; set; }
    public List<LeaderboardRowDto> Rows { get; set; } = new();
    public int? CurrentUserRank { get; set; }
}
