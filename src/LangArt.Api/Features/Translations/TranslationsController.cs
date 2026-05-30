using LangArt.Api.Features.Translations.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Translations;

[ApiController]
[Route("api/lesson-content")]
[Authorize]
public class TranslationsController : ControllerBase
{
    private readonly TranslationsService _svc;

    public TranslationsController(TranslationsService svc)
    {
        _svc = svc;
    }

    [HttpGet("{contentId:guid}/translations")]
    public Task<IReadOnlyList<TranslationDto>> List(Guid contentId, CancellationToken ct) =>
        _svc.ListForContentAsync(contentId, ct);

    [HttpPost("{contentId:guid}/translations")]
    [Authorize(Roles = "admin,teacher")]
    public Task<TranslationDto> Upsert(Guid contentId, [FromBody] UpsertTranslationRequest req, CancellationToken ct) =>
        _svc.UpsertAsync(contentId, req, ct);

    [HttpDelete("{contentId:guid}/translations/{language}")]
    [Authorize(Roles = "admin,teacher")]
    public async Task<IActionResult> Delete(Guid contentId, string language, CancellationToken ct)
    {
        await _svc.DeleteAsync(contentId, language, ct);
        return NoContent();
    }
}
