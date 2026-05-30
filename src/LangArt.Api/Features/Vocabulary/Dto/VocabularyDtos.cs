using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Vocabulary.Dto;

public class WordlistDto
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public string Level { get; set; } = "A1";
    public int WordCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PagedWordsDto
{
    public List<WordDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class WordDto
{
    public Guid Id { get; set; }
    public Guid WordlistId { get; set; }
    public string Term { get; set; } = string.Empty;
    public string TranslationUz { get; set; } = string.Empty;
    public string TranslationRu { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string? PronunciationUrl { get; set; }
    public string? ExampleSentence { get; set; }
    public string? PartOfSpeech { get; set; }
    public int Position { get; set; }
    public string? ImageUrl { get; set; }
    public int? FrequencyRank { get; set; }
    public string[]? TopicTags { get; set; }
}

public class UserWordlistEntryDto
{
    public Guid Id { get; set; }
    public WordDto Word { get; set; } = new();
    public string Status { get; set; } = "new";
    public DateTime? LastReviewedAt { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public DateTime AddedAt { get; set; }
    public int? DayNumber { get; set; }
    public string Source { get; set; } = "manual";
    public DateOnly? AssignedDate { get; set; }
}

public class CreateWordlistRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    [Required]
    public string Level { get; set; } = "A1";
}

public class UpdateWordlistRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    [Required]
    public string Level { get; set; } = "A1";
}

public class WordInput
{
    [Required, MinLength(1), MaxLength(200)]
    public string Term { get; set; } = string.Empty;

    [MaxLength(500)]
    public string TranslationUz { get; set; } = string.Empty;

    [MaxLength(500)]
    public string TranslationRu { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Definition { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? PronunciationUrl { get; set; }

    [MaxLength(2000)]
    public string? ExampleSentence { get; set; }

    [MaxLength(50)]
    public string? PartOfSpeech { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    public int? FrequencyRank { get; set; }

    public string[]? TopicTags { get; set; }
}

public class AddWordsRequest
{
    [Required, MinLength(1)]
    public List<WordInput> Words { get; set; } = new();
}

public class UpdateWordRequest : WordInput { }

public class AddToMyWordlistRequest
{
    /// <summary>If non-null, references an existing curated word.</summary>
    public Guid? WordId { get; set; }

    /// <summary>If <see cref="WordId"/> is null, an ad-hoc word is created in
    /// the user's private "personal" wordlist (auto-provisioned).</summary>
    public WordInput? AdHoc { get; set; }
}

public class UpdateEntryStatusRequest
{
    [Required]
    public string Status { get; set; } = "learning"; // new | learning | learned
}

public class FlashcardResultRequest
{
    [Required]
    public Guid WordId { get; set; }

    public bool Correct { get; set; }
}

public class FlashcardResultResponse
{
    public Guid EntryId { get; set; }
    public string Status { get; set; } = "new";
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public bool XpAwarded { get; set; }
}

public class DailyPackDto
{
    public int DayNumber { get; set; }
    public DateOnly AssignedDate { get; set; }
    public List<UserWordlistEntryDto> Words { get; set; } = new();
    public int Total { get; set; }
    public int Learned { get; set; }
    public int Learning { get; set; }
    public int NewCount { get; set; }
    public bool IsComplete { get; set; }
    /// <summary>Hybrid unlock: status of the previous day if it exists.</summary>
    public PreviousDayStatusDto? PreviousDayStatus { get; set; }
    /// <summary>Was the pack created in this request, or did it already exist?</summary>
    public bool JustCreated { get; set; }
}

public class PreviousDayStatusDto
{
    public int DayNumber { get; set; }
    public int Total { get; set; }
    public int Learned { get; set; }
    public bool IsComplete { get; set; }
}

public class DayProgressDto
{
    public int DayNumber { get; set; }
    public DateOnly AssignedDate { get; set; }
    public int Total { get; set; }
    public int Learned { get; set; }
    public int Learning { get; set; }
    public int NewCount { get; set; }
    public bool IsToday { get; set; }
    public bool IsComplete { get; set; }
}

public class AdminStudentVocabDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string VocabTargetLevel { get; set; } = "A1";
    public int? CurrentDayNumber { get; set; }
    public int TodayLearned { get; set; }
    public int TodayTotal { get; set; }
    public int TotalLearned { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public class SetVocabLevelRequest
{
    [Required]
    public string Level { get; set; } = "A1";
}
