using LangArt.Api.Features.Analytics.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Analytics;

/// <summary>
/// Admin-only teacher quality dashboard. Lists teachers with aggregate
/// metrics (group count, student count, avg student skill level, review
/// throughput, response time) and a detail page that drills into the
/// teacher's roster.
/// </summary>
[ApiController]
[Route("api/admin/teachers")]
[Authorize(Roles = "admin")]
public class AdminTeachersController : ControllerBase
{
    private readonly AnalyticsService _svc;

    public AdminTeachersController(AnalyticsService svc)
    {
        _svc = svc;
    }

    [HttpGet("")]
    public Task<IReadOnlyList<TeacherSummaryDto>> List(CancellationToken ct) =>
        _svc.ListTeachersAsync(ct);

    [HttpGet("{teacherId:guid}")]
    public Task<TeacherDetailDto> GetById(Guid teacherId, CancellationToken ct) =>
        _svc.GetTeacherAsync(teacherId, ct);
}
