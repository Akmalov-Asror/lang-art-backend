using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Courses.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Courses;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly CoursesService _courses;
    private readonly ICurrentUser _currentUser;

    public CoursesController(CoursesService courses, ICurrentUser currentUser)
    {
        _courses = courses;
        _currentUser = currentUser;
    }

    // ---------------- Courses ----------------

    [AllowAnonymous]
    [HttpGet("")]
    public Task<List<CourseResponse>> List() => _courses.ListAsync();

    [Authorize]
    [HttpGet("enrolled")]
    public Task<List<CourseResponse>> Enrolled() =>
        _courses.ListEnrolledForUserAsync(_currentUser.Id);

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public Task<CourseWithModulesResponse> GetById(Guid id) =>
        _courses.GetByIdAsync(id);

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("")]
    public Task<CourseResponse> Create([FromBody] CreateCourseRequest dto) =>
        _courses.CreateAsync(dto);

    [Authorize(Roles = "admin,teacher")]
    [HttpPut("{id:guid}")]
    public Task<CourseResponse> Update(Guid id, [FromBody] UpdateCourseRequest dto) =>
        _courses.UpdateAsync(id, dto);

    [Authorize(Roles = "admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _courses.DeleteCourseAsync(id);
        return Ok(new { });
    }

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("{id:guid}/duplicate")]
    public Task<CourseResponse> Duplicate(Guid id) =>
        _courses.DuplicateAsync(id);

    // ---------------- Modules ----------------

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("{courseId:guid}/modules")]
    public Task<ModuleResponse> CreateModule(Guid courseId, [FromBody] CreateModuleRequest dto) =>
        _courses.CreateModuleAsync(courseId, dto);

    [Authorize(Roles = "admin,teacher")]
    [HttpPut("modules/{moduleId:guid}")]
    public Task<ModuleResponse> UpdateModule(Guid moduleId, [FromBody] UpdateModuleRequest dto) =>
        _courses.UpdateModuleAsync(moduleId, dto);

    [Authorize(Roles = "admin,teacher")]
    [HttpDelete("modules/{moduleId:guid}")]
    public async Task<IActionResult> DeleteModule(Guid moduleId)
    {
        await _courses.DeleteModuleAsync(moduleId);
        return Ok(new { });
    }

    // ---------------- Lessons ----------------

    [Authorize]
    [HttpGet("lessons/{lessonId:guid}")]
    public Task<LessonWithContentResponse> GetLesson(Guid lessonId) =>
        _courses.GetLessonAsync(lessonId);

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("modules/{moduleId:guid}/lessons")]
    public Task<LessonResponse> CreateLesson(Guid moduleId, [FromBody] CreateLessonRequest dto) =>
        _courses.CreateLessonAsync(moduleId, dto);

    [Authorize(Roles = "admin,teacher")]
    [HttpPut("lessons/{lessonId:guid}")]
    public Task<LessonResponse> UpdateLesson(Guid lessonId, [FromBody] UpdateLessonRequest dto) =>
        _courses.UpdateLessonAsync(lessonId, dto);

    [Authorize(Roles = "admin,teacher")]
    [HttpDelete("lessons/{lessonId:guid}")]
    public async Task<IActionResult> DeleteLesson(Guid lessonId)
    {
        await _courses.DeleteLessonAsync(lessonId);
        return Ok(new { });
    }

    // ---------------- Lesson content ----------------

    [Authorize]
    [HttpGet("content/{contentId:guid}")]
    public Task<LessonContentResponse> GetContent(Guid contentId) =>
        _courses.GetLessonContentByIdAsync(contentId);

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("lessons/{lessonId:guid}/content")]
    public Task<LessonContentResponse> CreateContent(Guid lessonId, [FromBody] CreateLessonContentRequest dto) =>
        _courses.CreateLessonContentAsync(lessonId, dto);

    [Authorize(Roles = "admin,teacher")]
    [HttpPut("content/{contentId:guid}")]
    public Task<LessonContentResponse> UpdateContent(Guid contentId, [FromBody] UpdateLessonContentRequest dto) =>
        _courses.UpdateLessonContentAsync(contentId, dto);

    [Authorize(Roles = "admin,teacher")]
    [HttpDelete("content/{contentId:guid}")]
    public async Task<IActionResult> DeleteContent(Guid contentId)
    {
        await _courses.DeleteLessonContentAsync(contentId);
        return Ok(new { });
    }

    // ---------------- Resources ----------------

    [Authorize]
    [HttpGet("lessons/{lessonId:guid}/resources")]
    public Task<List<LessonResourceResponse>> GetResources(Guid lessonId) =>
        _courses.GetLessonResourcesAsync(lessonId);

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("lessons/{lessonId:guid}/resources")]
    public Task<LessonResourceResponse> CreateResource(Guid lessonId, [FromBody] CreateLessonResourceRequest dto) =>
        _courses.CreateLessonResourceAsync(lessonId, dto);

    [Authorize(Roles = "admin,teacher")]
    [HttpDelete("resources/{resourceId:guid}")]
    public async Task<IActionResult> DeleteResource(Guid resourceId)
    {
        await _courses.DeleteLessonResourceAsync(resourceId);
        return Ok(new { });
    }
}
