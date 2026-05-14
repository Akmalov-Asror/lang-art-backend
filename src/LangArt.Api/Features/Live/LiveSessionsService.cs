using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Live.Dto;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LangArt.Api.Features.Live;

/// <summary>
/// Lifecycle layer for Live Lesson Mode. Owns the rules around starting / ending
/// a teacher-led session; the real-time SignalR layer (next task) will sit on top.
/// </summary>
public class LiveSessionsService
{
    private const string OneActivePerClassroomIndex = "ix_live_sessions_one_active_per_classroom";

    private static readonly HashSet<string> ValidEndReasons = new(StringComparer.Ordinal)
    {
        "teacher_ended", "timeout", "server_restart",
    };

    private readonly AppDbContext _db;
    private readonly SessionRegistry _registry;

    public LiveSessionsService(AppDbContext db, SessionRegistry registry)
    {
        _db = db;
        _registry = registry;
    }

    public async Task<LiveSession> StartAsync(Guid classroomId, Guid lessonId, Guid actorId, string actorRole)
    {
        var classroom = await _db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == classroomId)
            ?? throw new NotFoundException("Classroom not found");

        // Teachers may only start sessions for their own classrooms; admins may start any.
        if (actorRole == "teacher" && classroom.TeacherId != actorId)
            throw new ForbiddenException("You can only start live sessions for your own classroom");

        // The lesson must belong to a course assigned to this classroom.
        var lessonCourseId = await _db.Lessons
            .Where(l => l.Id == lessonId)
            .Select(l => (Guid?)l.Module.CourseId)
            .FirstOrDefaultAsync();
        if (lessonCourseId is null)
            throw new NotFoundException("Lesson not found");

        var courseAssigned = await _db.GroupCourses
            .AnyAsync(gc => gc.GroupId == classroomId && gc.CourseId == lessonCourseId.Value);
        if (!courseAssigned)
            throw new BadRequestException("Lesson does not belong to a course assigned to this classroom");

        // Cheap pre-check so the common case returns a friendly 409 without depending on
        // the unique-index exception path. The unique index is still the source of truth
        // for the race window — see catch below.
        var existing = await _db.LiveSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ClassroomId == classroomId && s.EndedAt == null);
        if (existing is not null)
            throw new ConflictException(
                "Active session already exists",
                "active_session_exists",
                new { existingSessionId = existing.Id });

        var teacherId = actorRole == "teacher" ? actorId : classroom.TeacherId;
        var session = new LiveSession
        {
            ClassroomId = classroomId,
            LessonId = lessonId,
            TeacherId = teacherId,
            StartedAt = DateTime.UtcNow,
            CurrentBlockIndex = 0,
        };
        _db.LiveSessions.Add(session);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsActiveSessionUniqueViolation(ex))
        {
            // Concurrent insert won the race — look up the surviving row and report it.
            _db.Entry(session).State = EntityState.Detached;
            var current = await _db.LiveSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ClassroomId == classroomId && s.EndedAt == null);
            throw new ConflictException(
                "Active session already exists",
                "active_session_exists",
                new { existingSessionId = current?.Id });
        }

        return session;
    }

    public async Task<LiveSession> EndAsync(Guid sessionId, Guid actorId, string actorRole, string? reason)
    {
        var session = await _db.LiveSessions.FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new NotFoundException("Session not found");

        if (actorRole != "admin" && session.TeacherId != actorId)
            throw new ForbiddenException("Only the teacher who started the session, or an admin, can end it");

        if (session.EndedAt is not null)
            throw new BadRequestException("Session is already ended");

        var normalized = reason ?? "teacher_ended";
        if (!ValidEndReasons.Contains(normalized))
            throw new BadRequestException($"Invalid reason. Must be one of: {string.Join(", ", ValidEndReasons)}");

        session.EndedAt = DateTime.UtcNow;
        session.EndReason = normalized;
        await _db.SaveChangesAsync();

        // Drop the hot in-memory copy so subsequent JoinSession RPCs re-consult the DB and
        // see the just-written EndedAt, which makes the load return null and the hub reject.
        _registry.Evict(session.Id);

        return session;
    }

    public Task<LiveSession?> GetActiveAsync(Guid classroomId) =>
        _db.LiveSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ClassroomId == classroomId && s.EndedAt == null);

    public Task<LiveSession?> GetByIdAsync(Guid sessionId) =>
        _db.LiveSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

    public async Task<PagedResult<LiveSession>> GetHistoryAsync(Guid classroomId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var baseQuery = _db.LiveSessions
            .AsNoTracking()
            .Where(s => s.ClassroomId == classroomId);

        var total = await baseQuery.CountAsync();
        var items = await baseQuery
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<LiveSession>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total,
        };
    }

    /// <summary>Membership check used by the "members of the classroom" routes.</summary>
    public async Task EnsureClassroomMemberAsync(Guid classroomId, Guid userId, string role)
    {
        if (role == "admin") return;

        if (role == "teacher")
        {
            var isTeacher = await _db.Groups
                .AnyAsync(g => g.Id == classroomId && g.TeacherId == userId);
            if (!isTeacher) throw new ForbiddenException("Not a member of this classroom");
            return;
        }

        // student
        var enrolled = await _db.GroupStudents
            .AnyAsync(gs => gs.GroupId == classroomId && gs.StudentId == userId);
        if (!enrolled) throw new ForbiddenException("Not a member of this classroom");
    }

    private static bool IsActiveSessionUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg
        && pg.SqlState == PostgresErrorCodes.UniqueViolation
        && string.Equals(pg.ConstraintName, OneActivePerClassroomIndex, StringComparison.Ordinal);
}
