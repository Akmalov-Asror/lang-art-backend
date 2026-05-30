using LangArt.Api.Features.Vocabulary.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Vocabulary;

/// <summary>
/// Admin-only management for the daily vocabulary system. Lets admins inspect
/// each student's progress, change their CEFR target level, and reset today's
/// auto-assigned pack when needed.
/// </summary>
[ApiController]
[Route("api/admin/vocabulary/daily")]
[Authorize(Roles = "admin")]
public class AdminDailyVocabularyController : ControllerBase
{
    private readonly IDailyVocabularyService _daily;

    public AdminDailyVocabularyController(IDailyVocabularyService daily)
    {
        _daily = daily;
    }

    /// <summary>List every student with their current daily vocab snapshot.</summary>
    [HttpGet("students")]
    public Task<IReadOnlyList<AdminStudentVocabDto>> ListStudents(CancellationToken ct) =>
        _daily.GetAdminStudentsListAsync(ct);

    /// <summary>Change a student's CEFR target level (drives daily word selection).</summary>
    [HttpPatch("students/{studentId:guid}/level")]
    public async Task<IActionResult> SetLevel(Guid studentId, [FromBody] SetVocabLevelRequest req, CancellationToken ct)
    {
        await _daily.SetStudentLevelAsync(studentId, req.Level, ct);
        return NoContent();
    }

    /// <summary>Delete today's system-auto-assigned pack so it regenerates with current settings.</summary>
    [HttpPost("students/{studentId:guid}/reset-today")]
    public async Task<IActionResult> ResetToday(Guid studentId, CancellationToken ct)
    {
        var deleted = await _daily.ResetStudentTodayAsync(studentId, ct);
        return Ok(new { deleted });
    }
}
