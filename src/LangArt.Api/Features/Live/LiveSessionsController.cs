using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Live.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Live;

[ApiController]
[Route("api/live-sessions")]
public class LiveSessionsController : ControllerBase
{
    private readonly LiveSessionsService _service;
    private readonly ICurrentUser _currentUser;

    public LiveSessionsController(LiveSessionsService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("")]
    public async Task<LiveSessionResponse> Start([FromBody] StartLiveSessionRequest dto)
    {
        var session = await _service.StartAsync(dto.ClassroomId, dto.LessonId, _currentUser.Id, _currentUser.Role);
        return ToResponse(session);
    }

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("{id:guid}/end")]
    public async Task<LiveSessionResponse> End(Guid id, [FromBody] EndLiveSessionRequest? dto)
    {
        var session = await _service.EndAsync(id, _currentUser.Id, _currentUser.Role, dto?.Reason);
        return ToResponse(session);
    }

    [HttpGet("active")]
    public async Task<LiveSessionResponse?> GetActive([FromQuery] Guid classroomId)
    {
        await _service.EnsureClassroomMemberAsync(classroomId, _currentUser.Id, _currentUser.Role);
        var session = await _service.GetActiveAsync(classroomId);
        return session is null ? null : ToResponse(session);
    }

    [HttpGet("{id:guid}")]
    public async Task<LiveSessionResponse> GetById(Guid id)
    {
        var session = await _service.GetByIdAsync(id)
            ?? throw new Common.Exceptions.NotFoundException("Session not found");
        await _service.EnsureClassroomMemberAsync(session.ClassroomId, _currentUser.Id, _currentUser.Role);
        return ToResponse(session);
    }

    // History endpoint lives under /api/classrooms/{classroomId}/live-sessions per the
    // feature spec; using an absolute route here overrides the controller's "api/live-sessions" prefix.
    [Authorize(Roles = "admin,teacher")]
    [HttpGet("/api/classrooms/{classroomId:guid}/live-sessions")]
    public async Task<PagedResult<LiveSessionResponse>> GetHistory(
        Guid classroomId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        await _service.EnsureClassroomMemberAsync(classroomId, _currentUser.Id, _currentUser.Role);
        var paged = await _service.GetHistoryAsync(classroomId, page, pageSize);
        return new PagedResult<LiveSessionResponse>
        {
            Items = paged.Items.Select(ToResponse).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            Total = paged.Total,
        };
    }

    private static LiveSessionResponse ToResponse(LiveSession s) => new()
    {
        Id = s.Id,
        ClassroomId = s.ClassroomId,
        LessonId = s.LessonId,
        TeacherId = s.TeacherId,
        StartedAt = s.StartedAt,
        EndedAt = s.EndedAt,
        CurrentBlockIndex = s.CurrentBlockIndex,
        EndReason = s.EndReason,
    };
}
