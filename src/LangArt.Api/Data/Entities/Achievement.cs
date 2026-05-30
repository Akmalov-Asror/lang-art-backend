namespace LangArt.Api.Data.Entities;

/// <summary>
/// A monthly recognition awarded to a student. Unlike <see cref="Badge"/>
/// (criteria-based, can be earned at any time), an Achievement is tied to a
/// specific period (year + month) and category, with at most one winner per
/// (category, period). Computed by <c>AchievementsService</c> on demand.
/// </summary>
public class Achievement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>
    /// One of: student_of_month, best_speaker, best_writer, best_reader,
    /// best_listener, best_grammarian, best_vocabularist.
    /// </summary>
    public string Category { get; set; } = string.Empty;
    /// <summary>Year in UTC.</summary>
    public int PeriodYear { get; set; }
    /// <summary>Month in UTC (1-12).</summary>
    public int PeriodMonth { get; set; }
    /// <summary>Score used to pick this winner (e.g. 96 for 96/100 in speaking).</summary>
    public int Score { get; set; }
    public DateTime AwardedAt { get; set; }

    public Profile User { get; set; } = null!;
}
