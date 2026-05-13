using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Classroom.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Classroom;

public class ClassroomService
{
    private readonly AppDbContext _db;
    private readonly Features.Notifications.NotificationsService _notify;

    public ClassroomService(AppDbContext db, Features.Notifications.NotificationsService notify)
    {
        _db = db;
        _notify = notify;
    }

    // ---------------- Groups ----------------

    public async Task<List<GroupResponse>> ListGroupsAsync(Guid? teacherId)
    {
        var query = _db.Groups
            .AsNoTracking()
            .Include(g => g.Teacher)
            .Include(g => g.GroupStudents)
            .AsQueryable();

        if (teacherId.HasValue) query = query.Where(g => g.TeacherId == teacherId.Value);

        var rows = await query.OrderByDescending(g => g.CreatedAt).ToListAsync();
        return rows.Select(ToGroupResponse).ToList();
    }

    public async Task<GroupResponse> GetGroupAsync(Guid groupId)
    {
        var group = await _db.Groups
            .AsNoTracking()
            .Include(g => g.Teacher)
            .Include(g => g.GroupStudents)
            .FirstOrDefaultAsync(g => g.Id == groupId)
            ?? throw new NotFoundException("Group not found");
        return ToGroupResponse(group);
    }

    public async Task<GroupResponse> CreateGroupAsync(CreateGroupRequest dto)
    {
        var teacher = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == dto.TeacherId)
                      ?? throw new BadRequestException("Teacher not found");
        if (teacher.Role != Role.Teacher && teacher.Role != Role.Admin)
            throw new BadRequestException("Selected user is not a teacher");

        var group = new Group
        {
            Name = dto.Name,
            TeacherId = dto.TeacherId,
            ScheduleInfo = dto.ScheduleInfo,
            ScheduleDays = dto.ScheduleDays ?? Array.Empty<string>(),
            StartTime = ParseTime(dto.StartTime),
            EndTime = ParseTime(dto.EndTime),
            StartDate = dto.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = dto.IsActive ?? true,
        };
        _db.Groups.Add(group);
        await _db.SaveChangesAsync();
        return await GetGroupAsync(group.Id);
    }

    public async Task<GroupResponse> UpdateGroupAsync(Guid groupId, UpdateGroupRequest dto)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId)
                    ?? throw new NotFoundException("Group not found");

        if (dto.Name is not null) group.Name = dto.Name;
        if (dto.TeacherId.HasValue) group.TeacherId = dto.TeacherId.Value;
        if (dto.ScheduleInfo is not null) group.ScheduleInfo = dto.ScheduleInfo;
        if (dto.ScheduleDays is not null) group.ScheduleDays = dto.ScheduleDays;
        if (dto.StartTime is not null) group.StartTime = ParseTime(dto.StartTime);
        if (dto.EndTime is not null) group.EndTime = ParseTime(dto.EndTime);
        if (dto.StartDate.HasValue) group.StartDate = dto.StartDate.Value;
        if (dto.IsActive.HasValue) group.IsActive = dto.IsActive.Value;

        await _db.SaveChangesAsync();
        return await GetGroupAsync(group.Id);
    }

    public async Task DeleteGroupAsync(Guid groupId)
    {
        var deleted = await _db.Groups.Where(g => g.Id == groupId).ExecuteDeleteAsync();
        if (deleted == 0) throw new NotFoundException("Group not found");
    }

    // ---------------- Group students ----------------

    public async Task<List<GroupStudentWithProfileResponse>> GetGroupStudentsAsync(Guid groupId)
    {
        var rows = await _db.GroupStudents
            .AsNoTracking()
            .Where(gs => gs.GroupId == groupId)
            .Include(gs => gs.Student)
            .OrderBy(gs => gs.JoinedAt)
            .ToListAsync();

        return rows.Select(gs => new GroupStudentWithProfileResponse
        {
            Id = gs.Id,
            GroupId = gs.GroupId,
            StudentId = gs.StudentId,
            JoinedAt = gs.JoinedAt,
            Student = new ProfileSummary
            {
                Id = gs.Student.Id,
                Email = gs.Student.Email,
                FullName = gs.Student.FullName,
                Role = gs.Student.Role.ToString().ToLowerInvariant(),
                IsActive = gs.Student.IsActive,
                CreatedAt = gs.Student.CreatedAt,
                UpdatedAt = gs.Student.UpdatedAt,
            },
        }).ToList();
    }

    public async Task AddStudentToGroupAsync(Guid groupId, Guid studentId)
    {
        var groupExists = await _db.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists) throw new NotFoundException("Group not found");
        var studentExists = await _db.Profiles.AnyAsync(p => p.Id == studentId);
        if (!studentExists) throw new NotFoundException("Student not found");

        var dup = await _db.GroupStudents.AnyAsync(gs => gs.GroupId == groupId && gs.StudentId == studentId);
        if (dup) throw new ConflictException("Student already in group");

        _db.GroupStudents.Add(new GroupStudent { GroupId = groupId, StudentId = studentId });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveStudentFromGroupAsync(Guid groupId, Guid studentId)
    {
        var deleted = await _db.GroupStudents
            .Where(gs => gs.GroupId == groupId && gs.StudentId == studentId)
            .ExecuteDeleteAsync();
        if (deleted == 0) throw new NotFoundException("Student is not in this group");
    }

    // ---------------- Group courses ----------------

    public async Task<List<GroupCourseResponse>> GetGroupCoursesAsync(Guid groupId)
    {
        var rows = await _db.GroupCourses
            .AsNoTracking()
            .Where(gc => gc.GroupId == groupId)
            .Include(gc => gc.Course)
            .OrderBy(gc => gc.AssignedAt)
            .ToListAsync();

        return rows.Select(gc => new GroupCourseResponse
        {
            GroupId = gc.GroupId,
            CourseId = gc.CourseId,
            AssignedAt = gc.AssignedAt,
            Course = new CourseSummary
            {
                Id = gc.Course.Id,
                Title = gc.Course.Title,
                ThumbnailUrl = gc.Course.ThumbnailUrl,
            },
        }).ToList();
    }

    public async Task AssignCourseToGroupAsync(Guid groupId, Guid courseId)
    {
        var dup = await _db.GroupCourses.AnyAsync(gc => gc.GroupId == groupId && gc.CourseId == courseId);
        if (dup) throw new ConflictException("Course already assigned to group");

        _db.GroupCourses.Add(new GroupCourse { GroupId = groupId, CourseId = courseId });
        await _db.SaveChangesAsync();

        // Fan out a notification to every student in the group.
        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId);
        var studentIds = await _db.GroupStudents
            .Where(gs => gs.GroupId == groupId)
            .Select(gs => gs.StudentId)
            .ToListAsync();
        foreach (var sid in studentIds)
        {
            await _notify.NotifyAsync(
                sid,
                "course_assigned",
                "A new course was assigned to you",
                course is null ? null : $"\"{course.Title}\" is now available in your dashboard.",
                "/dashboard");
        }
    }

    public async Task RemoveCourseFromGroupAsync(Guid groupId, Guid courseId)
    {
        var deleted = await _db.GroupCourses
            .Where(gc => gc.GroupId == groupId && gc.CourseId == courseId)
            .ExecuteDeleteAsync();
        if (deleted == 0) throw new NotFoundException("Course is not assigned to this group");
    }

    // ---------------- Attendance ----------------

    public async Task<AttendanceResponse> MarkAttendanceAsync(MarkAttendanceRequest dto, Guid? createdBy)
    {
        var record = await _db.Attendance
            .FirstOrDefaultAsync(a => a.GroupId == dto.GroupId && a.StudentId == dto.StudentId && a.Date == dto.Date);

        if (record is null)
        {
            record = new Attendance
            {
                GroupId = dto.GroupId,
                StudentId = dto.StudentId,
                Date = dto.Date,
                Status = dto.Status,
                Notes = dto.Notes,
                CreatedBy = createdBy,
            };
            _db.Attendance.Add(record);
        }
        else
        {
            record.Status = dto.Status;
            record.Notes = dto.Notes;
            record.CreatedBy = createdBy ?? record.CreatedBy;
        }
        await _db.SaveChangesAsync();

        await _notify.NotifyAsync(
            dto.StudentId,
            "attendance_marked",
            $"Attendance marked: {dto.Status.ToString().ToLowerInvariant()}",
            $"Your attendance for {dto.Date:yyyy-MM-dd} was recorded.",
            "/dashboard/attendance");

        return ToAttendanceResponse(record);
    }

    public async Task<List<AttendanceResponse>> MarkBatchAttendanceAsync(BatchAttendanceRequest dto, Guid? createdBy)
    {
        var results = new List<AttendanceResponse>(dto.Records.Count);
        foreach (var r in dto.Records)
        {
            results.Add(await MarkAttendanceAsync(r, createdBy));
        }
        return results;
    }

    public async Task<List<AttendanceResponse>> GetAttendanceAsync(Guid groupId, DateOnly? date, DateOnly? startDate, DateOnly? endDate)
    {
        var query = _db.Attendance.AsNoTracking().Where(a => a.GroupId == groupId);

        if (date.HasValue)
        {
            query = query.Where(a => a.Date == date.Value);
        }
        else if (startDate.HasValue && endDate.HasValue)
        {
            query = query.Where(a => a.Date >= startDate.Value && a.Date <= endDate.Value);
        }

        var rows = await query.OrderBy(a => a.Date).ToListAsync();
        return rows.Select(ToAttendanceResponse).ToList();
    }

    public async Task<List<AttendanceResponse>> GetStudentAttendanceAsync(Guid studentId)
    {
        var rows = await _db.Attendance
            .AsNoTracking()
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();
        return rows.Select(ToAttendanceResponse).ToList();
    }

    // ---------------- Helpers ----------------

    private static TimeOnly? ParseTime(string? input) =>
        string.IsNullOrWhiteSpace(input) ? null : TimeOnly.Parse(input);

    private static GroupResponse ToGroupResponse(Group g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        TeacherId = g.TeacherId,
        ScheduleInfo = g.ScheduleInfo,
        ScheduleDays = g.ScheduleDays ?? Array.Empty<string>(),
        StartTime = g.StartTime?.ToString("HH:mm"),
        EndTime = g.EndTime?.ToString("HH:mm"),
        StartDate = g.StartDate,
        IsActive = g.IsActive,
        CreatedAt = g.CreatedAt,
        Teacher = g.Teacher is null ? null : new TeacherSummary
        {
            Id = g.Teacher.Id,
            FullName = g.Teacher.FullName,
            Email = g.Teacher.Email,
        },
        GroupStudents = g.GroupStudents
            .Select(gs => new GroupStudentRowResponse
            {
                Id = gs.Id,
                GroupId = gs.GroupId,
                StudentId = gs.StudentId,
                JoinedAt = gs.JoinedAt,
            })
            .ToList(),
    };

    private static AttendanceResponse ToAttendanceResponse(Attendance a) => new()
    {
        Id = a.Id,
        GroupId = a.GroupId,
        StudentId = a.StudentId,
        Date = a.Date,
        Status = a.Status.ToString().ToLowerInvariant(),
        Notes = a.Notes,
        CreatedBy = a.CreatedBy,
        CreatedAt = a.CreatedAt,
    };
}
