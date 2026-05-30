using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Vocabulary.Dto;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Vocabulary;

public class DailyVocabularyService : IDailyVocabularyService
{
    public const int DailyWordCount = 12;
    private static readonly string[] LevelLadder = { "A1", "A2", "B1", "B2", "C1", "C2" };

    private readonly AppDbContext _db;
    private readonly ILogger<DailyVocabularyService> _logger;

    public DailyVocabularyService(AppDbContext db, ILogger<DailyVocabularyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DailyPackDto> GetOrCreateTodaysPackAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == userId, ct)
            ?? throw new NotFoundException("User not found");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Idempotent: if today's pack already exists, return it as-is.
        var existingToday = await LoadDayEntriesAsync(userId, today, ct);
        if (existingToday.Count > 0)
        {
            var dayNum = existingToday[0].DayNumber!.Value;
            return await BuildPackDtoAsync(userId, dayNum, today, existingToday, justCreated: false, ct);
        }

        // No pack today — create one.
        var nextDay = await GetNextDayNumberAsync(userId, ct);
        var pickedWords = await PickWordsForLevelAsync(userId, user.VocabTargetLevel, DailyWordCount, ct);

        if (pickedWords.Count == 0)
        {
            _logger.LogWarning(
                "Daily vocab: no words available for user {UserId} at level {Level} — pack not created",
                userId, user.VocabTargetLevel);
            return new DailyPackDto
            {
                DayNumber = nextDay,
                AssignedDate = today,
                Words = new List<UserWordlistEntryDto>(),
                JustCreated = false,
                PreviousDayStatus = await GetPreviousDayStatusAsync(userId, nextDay, ct),
            };
        }

        var newEntries = pickedWords.Select(w => new UserWordlistEntry
        {
            UserId = userId,
            WordId = w.Id,
            Status = "new",
            DayNumber = nextDay,
            Source = "system_auto",
            AssignedDate = today,
        }).ToList();
        _db.UserWordlistEntries.AddRange(newEntries);
        await _db.SaveChangesAsync(ct);

        var freshEntries = await LoadDayEntriesAsync(userId, today, ct);
        return await BuildPackDtoAsync(userId, nextDay, today, freshEntries, justCreated: true, ct);
    }

    public async Task<DailyPackDto> GetDayPackAsync(Guid userId, int dayNumber, CancellationToken ct)
    {
        var entries = await _db.UserWordlistEntries.AsNoTracking()
            .Where(e => e.UserId == userId && e.DayNumber == dayNumber)
            .Include(e => e.Word)
            .OrderBy(e => e.AddedAt)
            .ToListAsync(ct);

        if (entries.Count == 0)
            throw new NotFoundException($"No day {dayNumber} pack found for this user");

        var assignedDate = entries[0].AssignedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return await BuildPackDtoAsync(userId, dayNumber, assignedDate, entries, justCreated: false, ct);
    }

    public async Task<IReadOnlyList<DayProgressDto>> GetDaysTimelineAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var grouped = await _db.UserWordlistEntries.AsNoTracking()
            .Where(e => e.UserId == userId && e.DayNumber != null && e.AssignedDate != null)
            .GroupBy(e => new { Day = e.DayNumber!.Value, Date = e.AssignedDate!.Value })
            .Select(g => new
            {
                g.Key.Day,
                g.Key.Date,
                Total = g.Count(),
                Learned = g.Count(e => e.Status == "learned"),
                Learning = g.Count(e => e.Status == "learning"),
                NewCount = g.Count(e => e.Status == "new"),
            })
            .OrderBy(g => g.Day)
            .ToListAsync(ct);

        return grouped.Select(g => new DayProgressDto
        {
            DayNumber = g.Day,
            AssignedDate = g.Date,
            Total = g.Total,
            Learned = g.Learned,
            Learning = g.Learning,
            NewCount = g.NewCount,
            IsToday = g.Date == today,
            IsComplete = g.Total > 0 && g.Learned == g.Total,
        }).ToList();
    }

    private async Task<int> GetNextDayNumberAsync(Guid userId, CancellationToken ct)
    {
        var maxDay = await _db.UserWordlistEntries.AsNoTracking()
            .Where(e => e.UserId == userId && e.DayNumber != null)
            .Select(e => (int?)e.DayNumber)
            .MaxAsync(ct);
        return (maxDay ?? 0) + 1;
    }

    private async Task<List<Word>> PickWordsForLevelAsync(Guid userId, string targetLevel, int count, CancellationToken ct)
    {
        var alreadyAddedWordIds = await _db.UserWordlistEntries.AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => e.WordId)
            .ToListAsync(ct);
        var addedSet = alreadyAddedWordIds.ToHashSet();

        var levelsInOrder = ResolveLevelLadder(targetLevel);
        var picked = new List<Word>(count);

        foreach (var level in levelsInOrder)
        {
            if (picked.Count >= count) break;
            var remaining = count - picked.Count;

            // FrequencyRank: lower = more frequent (ASC NULLS LAST).
            // Position: stores Wisdom "star" (1-5) where HIGHER = more important —
            // hence DESC so high-priority words win the secondary sort.
            var candidates = await _db.Words.AsNoTracking()
                .Where(w => w.Wordlist.IsPublic && w.Wordlist.Level == level)
                .Where(w => !addedSet.Contains(w.Id))
                .OrderBy(w => w.FrequencyRank ?? int.MaxValue)
                .ThenByDescending(w => w.Position)
                .ThenBy(w => w.Id)
                .Take(remaining)
                .ToListAsync(ct);

            picked.AddRange(candidates);
            foreach (var c in candidates) addedSet.Add(c.Id);
        }

        return picked;
    }

    /// <summary>
    /// Returns the target level first, then walks up the CEFR ladder so a user
    /// at A1 who has exhausted A1 vocab falls through to A2, B1, etc. Unknown
    /// levels fall back to just trying that level alone (no ladder).
    /// </summary>
    private static IEnumerable<string> ResolveLevelLadder(string targetLevel)
    {
        var idx = Array.IndexOf(LevelLadder, targetLevel);
        if (idx < 0) return new[] { targetLevel };
        return LevelLadder.Skip(idx);
    }

    private async Task<List<UserWordlistEntry>> LoadDayEntriesAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        return await _db.UserWordlistEntries.AsNoTracking()
            .Where(e => e.UserId == userId && e.AssignedDate == date)
            .Include(e => e.Word)
            .OrderBy(e => e.AddedAt)
            .ToListAsync(ct);
    }

    private async Task<DailyPackDto> BuildPackDtoAsync(
        Guid userId,
        int dayNumber,
        DateOnly assignedDate,
        IReadOnlyList<UserWordlistEntry> entries,
        bool justCreated,
        CancellationToken ct)
    {
        var learned = entries.Count(e => e.Status == "learned");
        var learning = entries.Count(e => e.Status == "learning");
        var fresh = entries.Count(e => e.Status == "new");

        return new DailyPackDto
        {
            DayNumber = dayNumber,
            AssignedDate = assignedDate,
            Words = entries.Select(ToDto).ToList(),
            Total = entries.Count,
            Learned = learned,
            Learning = learning,
            NewCount = fresh,
            IsComplete = entries.Count > 0 && learned == entries.Count,
            PreviousDayStatus = await GetPreviousDayStatusAsync(userId, dayNumber, ct),
            JustCreated = justCreated,
        };
    }

    private async Task<PreviousDayStatusDto?> GetPreviousDayStatusAsync(Guid userId, int currentDay, CancellationToken ct)
    {
        if (currentDay <= 1) return null;
        var prevDay = currentDay - 1;
        var stats = await _db.UserWordlistEntries.AsNoTracking()
            .Where(e => e.UserId == userId && e.DayNumber == prevDay)
            .GroupBy(e => e.DayNumber)
            .Select(g => new
            {
                Total = g.Count(),
                Learned = g.Count(e => e.Status == "learned"),
            })
            .FirstOrDefaultAsync(ct);
        if (stats is null || stats.Total == 0) return null;
        return new PreviousDayStatusDto
        {
            DayNumber = prevDay,
            Total = stats.Total,
            Learned = stats.Learned,
            IsComplete = stats.Learned == stats.Total,
        };
    }

    // ===================================================================
    // Admin methods
    // ===================================================================

    public async Task<IReadOnlyList<AdminStudentVocabDto>> GetAdminStudentsListAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var students = await _db.Profiles.AsNoTracking()
            .Where(p => p.Role == Role.Student)
            .OrderBy(p => p.FullName)
            .Select(p => new
            {
                p.Id,
                p.FullName,
                p.Email,
                p.VocabTargetLevel,
                CurrentDayNumber = _db.UserWordlistEntries
                    .Where(e => e.UserId == p.Id && e.DayNumber != null)
                    .Select(e => (int?)e.DayNumber)
                    .Max(),
                TodayLearned = _db.UserWordlistEntries
                    .Count(e => e.UserId == p.Id && e.AssignedDate == today && e.Status == "learned"),
                TodayTotal = _db.UserWordlistEntries
                    .Count(e => e.UserId == p.Id && e.AssignedDate == today),
                TotalLearned = _db.UserWordlistEntries
                    .Count(e => e.UserId == p.Id && e.Status == "learned"),
                LastActivityAt = _db.UserWordlistEntries
                    .Where(e => e.UserId == p.Id && e.LastReviewedAt != null)
                    .Select(e => e.LastReviewedAt)
                    .Max(),
            })
            .ToListAsync(ct);

        return students.Select(s => new AdminStudentVocabDto
        {
            UserId = s.Id,
            FullName = s.FullName,
            Email = s.Email,
            VocabTargetLevel = s.VocabTargetLevel,
            CurrentDayNumber = s.CurrentDayNumber,
            TodayLearned = s.TodayLearned,
            TodayTotal = s.TodayTotal,
            TotalLearned = s.TotalLearned,
            LastActivityAt = s.LastActivityAt,
        }).ToList();
    }

    public async Task SetStudentLevelAsync(Guid studentId, string level, CancellationToken ct)
    {
        var validLevels = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
        if (!validLevels.Contains(level))
            throw new BadRequestException($"Invalid level. Must be one of: {string.Join(", ", validLevels)}");

        var student = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == studentId, ct)
            ?? throw new NotFoundException("Student not found");

        if (student.Role != Role.Student)
            throw new BadRequestException("Target user is not a student");

        student.VocabTargetLevel = level;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ResetStudentTodayAsync(Guid studentId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var deleted = await _db.UserWordlistEntries
            .Where(e => e.UserId == studentId
                     && e.AssignedDate == today
                     && e.Source == "system_auto")
            .ExecuteDeleteAsync(ct);
        return deleted;
    }

    private static UserWordlistEntryDto ToDto(UserWordlistEntry e) => new()
    {
        Id = e.Id,
        Word = new WordDto
        {
            Id = e.Word.Id,
            WordlistId = e.Word.WordlistId,
            Term = e.Word.Term,
            TranslationUz = e.Word.TranslationUz,
            TranslationRu = e.Word.TranslationRu,
            Definition = e.Word.Definition,
            PronunciationUrl = e.Word.PronunciationUrl,
            ExampleSentence = e.Word.ExampleSentence,
            PartOfSpeech = e.Word.PartOfSpeech,
            Position = e.Word.Position,
            ImageUrl = e.Word.ImageUrl,
            FrequencyRank = e.Word.FrequencyRank,
            TopicTags = e.Word.TopicTags,
        },
        Status = e.Status,
        LastReviewedAt = e.LastReviewedAt,
        CorrectCount = e.CorrectCount,
        IncorrectCount = e.IncorrectCount,
        AddedAt = e.AddedAt,
        DayNumber = e.DayNumber,
        Source = e.Source,
        AssignedDate = e.AssignedDate,
    };
}
