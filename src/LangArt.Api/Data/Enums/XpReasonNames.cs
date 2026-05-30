namespace LangArt.Api.Data.Enums;

/// <summary>
/// Snake_case wire/DB names for <see cref="XpReason"/>. Lives outside the EF
/// HasConversion lambda so it can use modern C# switch expressions — EF can't
/// translate switch expressions inside the value-converter lambda itself.
/// Used by both the EF conversion (<c>GamificationConfiguration.XpLedgerConfiguration</c>)
/// and the raw-SQL idempotency-checking path in <c>GamificationService</c>.
/// </summary>
public static class XpReasonNames
{
    public static string ToWire(XpReason reason) => reason switch
    {
        XpReason.LessonCompleted     => "lesson_completed",
        XpReason.QuizPassed          => "quiz_passed",
        XpReason.QuizPerfectBonus    => "quiz_perfect_bonus",
        XpReason.DailyLogin          => "daily_login",
        XpReason.StreakBonus         => "streak_bonus",
        XpReason.BadgeReward         => "badge_reward",
        XpReason.AdminAdjustment     => "admin_adjustment",
        XpReason.VocabularyMastered  => "vocabulary_mastered",
        XpReason.ReadingCompleted    => "reading_completed",
        XpReason.SpeakingCompleted   => "speaking_completed",
        XpReason.WritingCompleted    => "writing_completed",
        _ => reason.ToString().ToLowerInvariant(),
    };

    public static XpReason FromWire(string value) => value switch
    {
        "lesson_completed"     => XpReason.LessonCompleted,
        "quiz_passed"          => XpReason.QuizPassed,
        "quiz_perfect_bonus"   => XpReason.QuizPerfectBonus,
        "daily_login"          => XpReason.DailyLogin,
        "streak_bonus"         => XpReason.StreakBonus,
        "badge_reward"         => XpReason.BadgeReward,
        "admin_adjustment"     => XpReason.AdminAdjustment,
        "vocabulary_mastered"  => XpReason.VocabularyMastered,
        "reading_completed"    => XpReason.ReadingCompleted,
        "speaking_completed"   => XpReason.SpeakingCompleted,
        "writing_completed"    => XpReason.WritingCompleted,
        _ => Enum.Parse<XpReason>(value, true),
    };
}
