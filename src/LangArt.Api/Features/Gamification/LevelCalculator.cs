namespace LangArt.Api.Features.Gamification;

/// <summary>
/// Pure functions for converting between total XP and level.
///
/// The curve is <c>xpForLevel(n) = floor(50 * n^1.5)</c> — cumulative XP needed
/// to reach level <c>n</c>. A user starts at level 0 with 0 XP and crosses
/// into level 1 at 50 XP, level 2 at 141 XP, etc.
///
/// Reference samples from the sprint brief:
/// <list type="bullet">
///   <item>Level 1  → 50</item>
///   <item>Level 2  → 141</item>
///   <item>Level 5  → 559</item>
///   <item>Level 10 → 1581 (the brief lists 1580; the formula yields 1581)</item>
///   <item>Level 25 → 6250</item>
///   <item>Level 50 → 17677</item>
/// </list>
/// </summary>
public static class LevelCalculator
{
    /// <summary>XP needed to reach <paramref name="level"/> (cumulative).</summary>
    public static int GetXpForLevel(int level)
    {
        if (level <= 0) return 0;
        return (int)Math.Floor(50.0 * Math.Pow(level, 1.5));
    }

    /// <summary>
    /// Returns the highest level <c>n ≥ 0</c> such that
    /// <c>GetXpForLevel(n) ≤ totalXp</c>. A user with 0–49 XP is level 0;
    /// 50–140 XP is level 1; etc.
    /// </summary>
    public static int GetLevelFromXp(int totalXp)
    {
        if (totalXp < 50) return 0;
        // Closed-form inverse, then snap down to the nearest valid level to
        // shake out any floating-point overshoot near a threshold.
        var approx = (int)Math.Floor(Math.Pow(totalXp / 50.0, 2.0 / 3.0));
        // Walk down/up at most once each to land on the canonical level.
        while (approx > 0 && GetXpForLevel(approx) > totalXp) approx--;
        while (GetXpForLevel(approx + 1) <= totalXp) approx++;
        return approx;
    }

    /// <summary>
    /// Convenience triple often returned to the client:
    /// the current level, XP earned inside that level, and XP needed to hit the next.
    /// </summary>
    public static (int Level, int XpInLevel, int XpForNextLevel) Progress(int totalXp)
    {
        var lvl = GetLevelFromXp(totalXp);
        var floor = GetXpForLevel(lvl);
        var ceiling = GetXpForLevel(lvl + 1);
        return (lvl, totalXp - floor, ceiling - floor);
    }
}
