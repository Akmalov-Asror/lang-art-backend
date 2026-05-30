using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Features.Vocabulary.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Vocabulary;

[ApiController]
[Route("api/vocabulary")]
[Authorize]
public class VocabularyController : ControllerBase
{
    private readonly IVocabularyService _svc;
    private readonly IDailyVocabularyService _daily;
    private readonly ICurrentUser _currentUser;

    public VocabularyController(IVocabularyService svc, IDailyVocabularyService daily, ICurrentUser currentUser)
    {
        _svc = svc;
        _daily = daily;
        _currentUser = currentUser;
    }

    [HttpGet("wordlists")]
    public Task<IReadOnlyList<WordlistDto>> ListWordlists(CancellationToken ct) =>
        _svc.ListWordlistsAsync(_currentUser.Id, _currentUser.Role, ct);

    [HttpGet("wordlists/{id:guid}")]
    public Task<WordlistDto> GetWordlist(Guid id, CancellationToken ct) =>
        _svc.GetWordlistAsync(id, _currentUser.Id, _currentUser.Role, ct);

    [HttpPost("wordlists")]
    [Authorize(Roles = "admin,teacher")]
    public Task<WordlistDto> CreateWordlist([FromBody] CreateWordlistRequest req, CancellationToken ct) =>
        _svc.CreateWordlistAsync(_currentUser.Id, req, ct);

    [HttpPut("wordlists/{id:guid}")]
    [Authorize(Roles = "admin,teacher")]
    public Task<WordlistDto> UpdateWordlist(Guid id, [FromBody] UpdateWordlistRequest req, CancellationToken ct) =>
        _svc.UpdateWordlistAsync(id, _currentUser.Id, _currentUser.Role, req, ct);

    [HttpDelete("wordlists/{id:guid}")]
    [Authorize(Roles = "admin,teacher")]
    public async Task<IActionResult> DeleteWordlist(Guid id, CancellationToken ct)
    {
        await _svc.DeleteWordlistAsync(id, _currentUser.Id, _currentUser.Role, ct);
        return NoContent();
    }

    [HttpGet("wordlists/{id:guid}/words")]
    public Task<IReadOnlyList<WordDto>> ListWords(Guid id, CancellationToken ct) =>
        _svc.ListWordsAsync(id, _currentUser.Id, _currentUser.Role, ct);

    [HttpGet("wordlists/{id:guid}/words/paged")]
    public Task<PagedWordsDto> ListWordsPaged(Guid id, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        _svc.ListWordsPagedAsync(id, _currentUser.Id, _currentUser.Role, search, page, pageSize, ct);

    [HttpPost("wordlists/{id:guid}/words")]
    [Authorize(Roles = "admin,teacher")]
    public Task<IReadOnlyList<WordDto>> AddWords(Guid id, [FromBody] AddWordsRequest req, CancellationToken ct) =>
        _svc.AddWordsAsync(id, _currentUser.Id, _currentUser.Role, req, ct);

    [HttpPut("wordlists/{wordlistId:guid}/words/{wordId:guid}")]
    [Authorize(Roles = "admin,teacher")]
    public Task<WordDto> UpdateWord(Guid wordlistId, Guid wordId, [FromBody] UpdateWordRequest req, CancellationToken ct) =>
        _svc.UpdateWordAsync(wordlistId, wordId, _currentUser.Id, _currentUser.Role, req, ct);

    [HttpDelete("wordlists/{wordlistId:guid}/words/{wordId:guid}")]
    [Authorize(Roles = "admin,teacher")]
    public async Task<IActionResult> DeleteWord(Guid wordlistId, Guid wordId, CancellationToken ct)
    {
        await _svc.DeleteWordAsync(wordlistId, wordId, _currentUser.Id, _currentUser.Role, ct);
        return NoContent();
    }

    [HttpPost("my/add-word")]
    public Task<UserWordlistEntryDto> AddToMy([FromBody] AddToMyWordlistRequest req, CancellationToken ct) =>
        _svc.AddToMyWordlistAsync(_currentUser.Id, req, ct);

    [HttpGet("my/wordlist")]
    public Task<IReadOnlyList<UserWordlistEntryDto>> GetMy(CancellationToken ct) =>
        _svc.GetMyWordlistAsync(_currentUser.Id, ct);

    [HttpPatch("my/wordlist/{entryId:guid}")]
    public Task<UserWordlistEntryDto> UpdateMyStatus(Guid entryId, [FromBody] UpdateEntryStatusRequest req, CancellationToken ct) =>
        _svc.UpdateEntryStatusAsync(_currentUser.Id, entryId, req, ct);

    [HttpDelete("my/wordlist/{entryId:guid}")]
    public async Task<IActionResult> RemoveFromMy(Guid entryId, CancellationToken ct)
    {
        await _svc.RemoveFromMyWordlistAsync(_currentUser.Id, entryId, ct);
        return NoContent();
    }

    [HttpGet("my/review-due")]
    public Task<IReadOnlyList<UserWordlistEntryDto>> ReviewDue([FromQuery] int limit = 20, CancellationToken ct = default) =>
        _svc.GetReviewDueAsync(_currentUser.Id, limit, ct);

    [HttpPost("my/flashcard-result")]
    public Task<FlashcardResultResponse> FlashcardResult([FromBody] FlashcardResultRequest req, CancellationToken ct) =>
        _svc.RecordFlashcardResultAsync(_currentUser.Id, req, ct);

    /// <summary>Returns today's auto-assigned vocabulary pack (creates it lazily on first call of the day).</summary>
    [HttpGet("my/today")]
    public Task<DailyPackDto> GetTodaysPack(CancellationToken ct) =>
        _daily.GetOrCreateTodaysPackAsync(_currentUser.Id, ct);

    /// <summary>Returns the full per-day progress timeline for the current user.</summary>
    [HttpGet("my/days")]
    public Task<IReadOnlyList<DayProgressDto>> GetDaysTimeline(CancellationToken ct) =>
        _daily.GetDaysTimelineAsync(_currentUser.Id, ct);

    /// <summary>Returns the pack for a specific day number (history navigation).</summary>
    [HttpGet("my/day/{dayNumber:int}")]
    public Task<DailyPackDto> GetDayPack(int dayNumber, CancellationToken ct) =>
        _daily.GetDayPackAsync(_currentUser.Id, dayNumber, ct);
}
