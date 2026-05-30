using LangArt.Api.Features.Vocabulary.Dto;

namespace LangArt.Api.Features.Vocabulary;

/// <summary>
/// Auto-assigns a daily pack of vocabulary words to each student based on
/// their <c>vocab_target_level</c> and word frequency. Idempotent per-day:
/// calling <see cref="GetOrCreateTodaysPackAsync"/> multiple times in the
/// same UTC day returns the same words.
/// </summary>
public interface IDailyVocabularyService
{
    Task<DailyPackDto> GetOrCreateTodaysPackAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<DayProgressDto>> GetDaysTimelineAsync(Guid userId, CancellationToken ct);
    Task<DailyPackDto> GetDayPackAsync(Guid userId, int dayNumber, CancellationToken ct);

    // ===== Admin =====
    Task<IReadOnlyList<AdminStudentVocabDto>> GetAdminStudentsListAsync(CancellationToken ct);
    Task SetStudentLevelAsync(Guid studentId, string level, CancellationToken ct);
    Task<int> ResetStudentTodayAsync(Guid studentId, CancellationToken ct);
}
