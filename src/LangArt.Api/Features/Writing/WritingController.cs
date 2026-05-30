using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Writing.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Writing;

[ApiController]
[Route("api/writing")]
[Authorize]
public class WritingController : ControllerBase
{
    private readonly WritingService _svc;
    private readonly ICurrentUser _currentUser;

    public WritingController(WritingService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpPost("submit")]
    public Task<WritingSubmissionDto> Submit([FromBody] SubmitWritingRequest req, CancellationToken ct) =>
        _svc.SubmitAsync(_currentUser.Id, req, ct);

    [HttpGet("my/submissions")]
    public Task<IReadOnlyList<WritingSubmissionDto>> ListMine(CancellationToken ct) =>
        _svc.ListMineAsync(_currentUser.Id, ct);

    [HttpGet("submissions/{id:guid}")]
    public Task<WritingSubmissionDto> GetById(Guid id, CancellationToken ct) =>
        _svc.GetByIdAsync(id, _currentUser.Id, _currentUser.Role, ct);

    [HttpGet("review/queue")]
    [Authorize(Roles = "admin,teacher")]
    public Task<IReadOnlyList<WritingSubmissionDto>> ReviewQueue(CancellationToken ct) =>
        _svc.ListReviewQueueAsync(ct);

    [HttpPost("review/{id:guid}")]
    [Authorize(Roles = "admin,teacher")]
    public Task<WritingSubmissionDto> Review(Guid id, [FromBody] TeacherWritingReviewRequest req, CancellationToken ct) =>
        _svc.ReviewAsync(id, _currentUser.Id, req, ct);
}
