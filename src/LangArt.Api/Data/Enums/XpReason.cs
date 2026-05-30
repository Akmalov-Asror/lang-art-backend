namespace LangArt.Api.Data.Enums;

/// <summary>
/// Discriminator on <c>xp_ledger.reason</c>. Stored as a TEXT column with a
/// CHECK constraint (matching the existing pattern for <c>role</c> /
/// <c>payments.status</c>) — see <c>Data/Seeders/SeedRunner.EnsureSchemaUpgradesAsync</c>.
/// </summary>
public enum XpReason
{
    LessonCompleted,
    QuizPassed,
    QuizPerfectBonus,
    DailyLogin,
    StreakBonus,
    BadgeReward,
    AdminAdjustment,
    VocabularyMastered,
    ReadingCompleted,
    SpeakingCompleted,
    WritingCompleted,
}
