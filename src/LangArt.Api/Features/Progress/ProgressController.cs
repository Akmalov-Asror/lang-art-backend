using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Features.Progress.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Progress;

[ApiController]
[Route("api/progress")]
public class ProgressController : ControllerBase
{
    private readonly ProgressService _progress;
    private readonly ICurrentUser _currentUser;

    public ProgressController(ProgressService progress, ICurrentUser currentUser)
    {
        _progress = progress;
        _currentUser = currentUser;
    }

    // ---------------- Completions ----------------

    [Authorize]
    [HttpPost("lessons/{lessonId:guid}/complete")]
    public Task<LessonCompletionResponse> Complete(Guid lessonId) =>
        _progress.MarkCompleteAsync(_currentUser.Id, lessonId);

    [Authorize]
    [HttpGet("completions")]
    public Task<List<LessonCompletionResponse>> ListCompletions() =>
        _progress.ListCompletionsAsync(_currentUser.Id);

    [Authorize]
    [HttpGet("lessons/{lessonId:guid}/completed")]
    public Task<CompletedStatusResponse> IsCompleted(Guid lessonId) =>
        _progress.IsCompletedAsync(_currentUser.Id, lessonId);

    // ---------------- Quiz results ----------------

    [Authorize]
    [HttpPost("quiz-results")]
    public Task<QuizResultResponse> SubmitQuiz([FromBody] SubmitQuizResultRequest dto) =>
        _progress.SubmitQuizResultAsync(_currentUser.Id, dto);

    [Authorize]
    [HttpGet("quiz-results")]
    public Task<object?> GetQuizResults([FromQuery] Guid? lessonId, [FromQuery] Guid? contentId) =>
        _progress.GetQuizResultsAsync(_currentUser.Id, lessonId, contentId);

    // ---------------- Course progress ----------------

    [Authorize]
    [HttpGet("courses/{courseId:guid}")]
    public Task<CourseProgressResponse> CourseProgress(Guid courseId, [FromQuery] Guid? userId) =>
        _progress.GetCourseProgressAsync(ResolveTargetUserId(userId), courseId);

    [Authorize]
    [HttpGet("courses/{courseId:guid}/percentage")]
    public Task<CoursePercentageResponse> CoursePercentage(Guid courseId, [FromQuery] Guid? userId) =>
        _progress.GetCoursePercentageAsync(ResolveTargetUserId(userId), courseId);

    /// <summary>
    /// Used by the course-progress endpoints to support "teacher viewing a student's progress".
    /// Students can only ever read their own progress; admins/teachers may pass any userId.
    /// </summary>
    private Guid ResolveTargetUserId(Guid? requested)
    {
        if (!requested.HasValue || requested.Value == _currentUser.Id)
            return _currentUser.Id;

        if (_currentUser.Role == "student")
            throw new ForbiddenException("Cannot view another user's progress");

        return requested.Value;
    }

    // ---------------- Lesson access (admin/teacher writes; auth reads) ----------------

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("access/{studentId:guid}/{lessonId:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid studentId, Guid lessonId)
    {
        await _progress.UnlockAsync(studentId, lessonId, _currentUser.TryGetId());
        return Ok(new { });
    }

    [Authorize(Roles = "admin,teacher")]
    [HttpDelete("access/{studentId:guid}/{lessonId:guid}/lock")]
    public async Task<IActionResult> Lock(Guid studentId, Guid lessonId)
    {
        await _progress.LockAsync(studentId, lessonId);
        return Ok(new { });
    }

    [Authorize]
    [HttpGet("access/{studentId:guid}")]
    public Task<List<Guid>> GetUnlockedLessonIds(Guid studentId)
    {
        if (_currentUser.Role == "student" && _currentUser.Id != studentId)
            throw new ForbiddenException("Cannot view another student's access map");
        return _progress.GetUnlockedLessonIdsAsync(studentId);
    }

    [Authorize]
    [HttpGet("access/{studentId:guid}/{lessonId:guid}")]
    public Task<UnlockStatusResponse> GetUnlockStatus(Guid studentId, Guid lessonId)
    {
        if (_currentUser.Role == "student" && _currentUser.Id != studentId)
            throw new ForbiddenException("Cannot view another student's access status");
        return _progress.GetUnlockStatusAsync(studentId, lessonId);
    }

    // ---------------- Reporting (admin/teacher) ----------------

    [Authorize(Roles = "admin,teacher")]
    [HttpGet("groups/{groupId:guid}/results")]
    public Task<List<QuizResultResponse>> GroupResults(Guid groupId) =>
        _progress.GetGroupResultsAsync(groupId);

    [Authorize(Roles = "admin,teacher")]
    [HttpGet("students/{studentId:guid}/results")]
    public Task<List<QuizResultResponse>> StudentResults(Guid studentId) =>
        _progress.GetStudentResultsAsync(studentId);
}
