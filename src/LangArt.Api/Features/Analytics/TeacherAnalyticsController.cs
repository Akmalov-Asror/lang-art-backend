using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Analytics.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Analytics;

/// <summary>
/// Teacher-facing student analytics (per-skill levels, recent submissions,
/// notes). Separate from the existing <see cref="AnalyticsController"/> which
/// owns quiz-question stats.
/// </summary>
[ApiController]
[Route("api/teacher")]
[Authorize(Roles = "admin,teacher")]
public class TeacherAnalyticsController : ControllerBase
{
    private readonly AnalyticsService _svc;
    private readonly ICurrentUser _currentUser;

    public TeacherAnalyticsController(AnalyticsService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet("students")]
    public Task<IReadOnlyList<StudentSummaryDto>> ListStudents(CancellationToken ct) =>
        _svc.ListStudentsAsync(_currentUser.Id, _currentUser.Role, ct);

    [HttpGet("students/{studentId:guid}")]
    public Task<StudentDetailDto> GetStudent(Guid studentId, CancellationToken ct) =>
        _svc.GetStudentAsync(studentId, _currentUser.Id, _currentUser.Role, ct);

    [HttpGet("students/{studentId:guid}/submissions")]
    public Task<IReadOnlyList<RecentSubmissionDto>> RecentSubmissions(Guid studentId, [FromQuery] int limit = 50, CancellationToken ct = default) =>
        _svc.GetRecentSubmissionsAsync(studentId, limit, ct);

    [HttpGet("students/{studentId:guid}/notes")]
    public Task<IReadOnlyList<TeacherNoteDto>> ListNotes(Guid studentId, CancellationToken ct) =>
        _svc.ListNotesAsync(studentId, ct);

    [HttpPost("students/{studentId:guid}/notes")]
    public Task<TeacherNoteDto> AddNote(Guid studentId, [FromBody] UpsertNoteRequest req, CancellationToken ct) =>
        _svc.AddNoteAsync(studentId, _currentUser.Id, req, ct);

    [HttpDelete("notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid noteId, CancellationToken ct)
    {
        await _svc.DeleteNoteAsync(noteId, _currentUser.Id, _currentUser.Role, ct);
        return NoContent();
    }
}
