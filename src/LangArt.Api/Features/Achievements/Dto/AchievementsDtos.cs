namespace LangArt.Api.Features.Achievements.Dto;

public class AchievementDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public string CategoryLabel { get; set; } = string.Empty;
    public string CategoryEmoji { get; set; } = string.Empty;
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public int Score { get; set; }
    public DateTime AwardedAt { get; set; }
}

public class AchievementMonthlyDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty; // e.g. "May 2026"
    public List<AchievementDto> Awards { get; set; } = new();
}

public class ComputeAwardsResult
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int NewAwardsCount { get; set; }
    public List<AchievementDto> Awards { get; set; } = new();
}
