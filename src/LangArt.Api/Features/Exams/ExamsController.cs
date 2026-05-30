using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Exams.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Exams;

[ApiController]
[Route("api/exams")]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly ExamsService _svc;
    private readonly ICurrentUser _currentUser;

    public ExamsController(ExamsService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet("")]
    public Task<IReadOnlyList<ExamDto>> List(CancellationToken ct) =>
        _svc.ListAsync(_currentUser.Id, ct);

    [HttpGet("{id:guid}")]
    public Task<ExamDto> GetById(Guid id, CancellationToken ct) =>
        _svc.GetByIdAsync(id, _currentUser.Id, ct);

    [HttpPost("")]
    [Authorize(Roles = "admin,teacher")]
    public Task<ExamDto> Create([FromBody] CreateExamRequest req, CancellationToken ct) =>
        _svc.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,teacher")]
    public Task<ExamDto> Update(Guid id, [FromBody] UpdateExamRequest req, CancellationToken ct) =>
        _svc.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin,teacher")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    public Task<ExamAttemptDto> Submit(Guid id, [FromBody] SubmitExamRequest req, CancellationToken ct) =>
        _svc.SubmitAsync(_currentUser.Id, id, req, ct);

    [HttpGet("my/attempts")]
    public Task<IReadOnlyList<ExamAttemptDto>> MyAttempts(CancellationToken ct) =>
        _svc.MyAttemptsAsync(_currentUser.Id, ct);

    [HttpGet("kinds")]
    public IActionResult Kinds() =>
        Ok(ExamsService.KindLabels.Select(kv => new { code = kv.Key, label = kv.Value }));
}
