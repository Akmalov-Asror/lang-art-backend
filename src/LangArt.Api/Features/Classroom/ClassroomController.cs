using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Features.Classroom.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Classroom;

[ApiController]
[Route("api/classroom")]
public class ClassroomController : ControllerBase
{
    private readonly ClassroomService _classroom;
    private readonly ICurrentUser _currentUser;

    public ClassroomController(ClassroomService classroom, ICurrentUser currentUser)
    {
        _classroom = classroom;
        _currentUser = currentUser;
    }

    // ---------------- Groups ----------------

    [Authorize(Roles = "admin,teacher")]
    [HttpGet("groups")]
    public Task<List<GroupResponse>> ListGroups([FromQuery] Guid? teacherId)
    {
        // Teachers only see their own groups by default; admins see all.
        if (_currentUser.Role == "teacher")
        {
            return _classroom.ListGroupsAsync(_currentUser.Id);
        }
        return _classroom.ListGroupsAsync(teacherId);
    }

    [Authorize(Roles = "admin,teacher")]
    [HttpGet("groups/{groupId:guid}")]
    public Task<GroupResponse> GetGroup(Guid groupId) =>
        _classroom.GetGroupAsync(groupId);

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("groups")]
    public Task<GroupResponse> CreateGroup([FromBody] CreateGroupRequest dto) =>
        _classroom.CreateGroupAsync(dto);

    [Authorize(Roles = "admin,teacher")]
    [HttpPut("groups/{groupId:guid}")]
    public Task<GroupResponse> UpdateGroup(Guid groupId, [FromBody] UpdateGroupRequest dto) =>
        _classroom.UpdateGroupAsync(groupId, dto);

    [Authorize(Roles = "admin,teacher")]
    [HttpDelete("groups/{groupId:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid groupId)
    {
        await _classroom.DeleteGroupAsync(groupId);
        return Ok(new { });
    }

    // ---------------- Group students ----------------

    [Authorize(Roles = "admin,teacher")]
    [HttpGet("groups/{groupId:guid}/students")]
    public Task<List<GroupStudentWithProfileResponse>> GetGroupStudents(Guid groupId) =>
        _classroom.GetGroupStudentsAsync(groupId);

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("groups/{groupId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> AddStudent(Guid groupId, Guid studentId)
    {
        await _classroom.AddStudentToGroupAsync(groupId, studentId);
        return Ok(new { });
    }

    [Authorize(Roles = "admin,teacher")]
    [HttpDelete("groups/{groupId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> RemoveStudent(Guid groupId, Guid studentId)
    {
        await _classroom.RemoveStudentFromGroupAsync(groupId, studentId);
        return Ok(new { });
    }

    // ---------------- Group courses ----------------

    [Authorize(Roles = "admin,teacher")]
    [HttpGet("groups/{groupId:guid}/courses")]
    public Task<List<GroupCourseResponse>> GetGroupCourses(Guid groupId) =>
        _classroom.GetGroupCoursesAsync(groupId);

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("groups/{groupId:guid}/courses/{courseId:guid}")]
    public async Task<IActionResult> AssignCourse(Guid groupId, Guid courseId)
    {
        await _classroom.AssignCourseToGroupAsync(groupId, courseId);
        return Ok(new { });
    }

    [Authorize(Roles = "admin,teacher")]
    [HttpDelete("groups/{groupId:guid}/courses/{courseId:guid}")]
    public async Task<IActionResult> RemoveCourse(Guid groupId, Guid courseId)
    {
        await _classroom.RemoveCourseFromGroupAsync(groupId, courseId);
        return Ok(new { });
    }

    // ---------------- Attendance ----------------

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("attendance")]
    public Task<AttendanceResponse> MarkAttendance([FromBody] MarkAttendanceRequest dto) =>
        _classroom.MarkAttendanceAsync(dto, _currentUser.TryGetId());

    [Authorize(Roles = "admin,teacher")]
    [HttpPost("attendance/batch")]
    public Task<List<AttendanceResponse>> MarkBatchAttendance([FromBody] BatchAttendanceRequest dto) =>
        _classroom.MarkBatchAttendanceAsync(dto, _currentUser.TryGetId());

    [Authorize(Roles = "admin,teacher")]
    [HttpGet("attendance/{groupId:guid}")]
    public Task<List<AttendanceResponse>> GetAttendance(
        Guid groupId,
        [FromQuery] DateOnly? date,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate) =>
        _classroom.GetAttendanceAsync(groupId, date, startDate, endDate);

    [Authorize]
    [HttpGet("attendance/student/{studentId:guid}")]
    public Task<List<AttendanceResponse>> GetStudentAttendance(Guid studentId)
    {
        // Students may only see their own attendance; admins/teachers see anyone.
        if (_currentUser.Role == "student" && _currentUser.Id != studentId)
            throw new ForbiddenException("Cannot view another student's attendance");
        return _classroom.GetStudentAttendanceAsync(studentId);
    }
}
