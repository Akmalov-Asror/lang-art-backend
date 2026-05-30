using LangArt.Api.Features.Vocabulary.Dto;

namespace LangArt.Api.Features.Vocabulary;

public interface IVocabularyService
{
    Task<IReadOnlyList<WordlistDto>> ListWordlistsAsync(Guid currentUserId, string currentRole, CancellationToken ct);
    Task<WordlistDto> GetWordlistAsync(Guid id, Guid currentUserId, string currentRole, CancellationToken ct);
    Task<WordlistDto> CreateWordlistAsync(Guid ownerId, CreateWordlistRequest req, CancellationToken ct);
    Task<WordlistDto> UpdateWordlistAsync(Guid id, Guid currentUserId, string currentRole, UpdateWordlistRequest req, CancellationToken ct);
    Task DeleteWordlistAsync(Guid id, Guid currentUserId, string currentRole, CancellationToken ct);

    Task<IReadOnlyList<WordDto>> ListWordsAsync(Guid wordlistId, Guid currentUserId, string currentRole, CancellationToken ct);
    Task<PagedWordsDto> ListWordsPagedAsync(Guid wordlistId, Guid currentUserId, string currentRole, string? search, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<WordDto>> AddWordsAsync(Guid wordlistId, Guid currentUserId, string currentRole, AddWordsRequest req, CancellationToken ct);
    Task<WordDto> UpdateWordAsync(Guid wordlistId, Guid wordId, Guid currentUserId, string currentRole, UpdateWordRequest req, CancellationToken ct);
    Task DeleteWordAsync(Guid wordlistId, Guid wordId, Guid currentUserId, string currentRole, CancellationToken ct);

    Task<UserWordlistEntryDto> AddToMyWordlistAsync(Guid userId, AddToMyWordlistRequest req, CancellationToken ct);
    Task<IReadOnlyList<UserWordlistEntryDto>> GetMyWordlistAsync(Guid userId, CancellationToken ct);
    Task<UserWordlistEntryDto> UpdateEntryStatusAsync(Guid userId, Guid entryId, UpdateEntryStatusRequest req, CancellationToken ct);
    Task RemoveFromMyWordlistAsync(Guid userId, Guid entryId, CancellationToken ct);
    Task<IReadOnlyList<UserWordlistEntryDto>> GetReviewDueAsync(Guid userId, int limit, CancellationToken ct);
    Task<FlashcardResultResponse> RecordFlashcardResultAsync(Guid userId, FlashcardResultRequest req, CancellationToken ct);
}
