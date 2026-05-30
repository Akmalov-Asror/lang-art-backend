using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Speaking.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Speaking;

[ApiController]
[Route("api/speaking")]
[Authorize]
public class SpeakingController : ControllerBase
{
    private readonly SpeakingService _svc;
    private readonly ICurrentUser _currentUser;

    public SpeakingController(SpeakingService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpPost("submit")]
    public Task<SpeakingSubmissionDto> Submit([FromBody] SubmitSpeakingRequest req, CancellationToken ct) =>
        _svc.SubmitAsync(_currentUser.Id, req, ct);

    [HttpGet("my/submissions")]
    public Task<IReadOnlyList<SpeakingSubmissionDto>> ListMine(CancellationToken ct) =>
        _svc.ListMineAsync(_currentUser.Id, ct);

    [HttpGet("submissions/{id:guid}")]
    public Task<SpeakingSubmissionDto> GetById(Guid id, CancellationToken ct) =>
        _svc.GetByIdAsync(id, _currentUser.Id, _currentUser.Role, ct);

    [HttpGet("review/queue")]
    [Authorize(Roles = "admin,teacher")]
    public Task<IReadOnlyList<SpeakingSubmissionDto>> ReviewQueue(CancellationToken ct) =>
        _svc.ListReviewQueueAsync(ct);

    [HttpPost("review/{id:guid}")]
    [Authorize(Roles = "admin,teacher")]
    public Task<SpeakingSubmissionDto> Review(Guid id, [FromBody] TeacherReviewRequest req, CancellationToken ct) =>
        _svc.ReviewAsync(id, _currentUser.Id, req, ct);
}
