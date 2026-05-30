namespace LangArt.Api.Data.Entities;

/// <summary>
/// Daily-activity streak. <c>LastActivityDateUtc</c> is a calendar date in UTC —
/// streak math is "if last activity was yesterday, ++ ; if older, reset to 1 ;
/// if today, no-op". The hourly <see cref="Features.Gamification.StreakResetService"/>
/// zeroes <c>CurrentStreak</c> for anyone who fell off the wagon.
/// </summary>
public class UserStreak
{
    public Guid UserId { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateOnly LastActivityDateUtc { get; set; }

    public Profile User { get; set; } = null!;
}
